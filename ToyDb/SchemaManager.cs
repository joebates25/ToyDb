using ToyDb.Pages;

namespace ToyDb;

using System.Text;

public class SchemaManager(PageBufferManager pageBufferManager)
{
    private static readonly StringComparer NameComparer = StringComparer.Ordinal;

    private readonly Dictionary<string, SchemaEntry> _schemaDirectory = LoadSchemaDirectory(pageBufferManager);

    public bool HasSchema(string schemaName) => _schemaDirectory.ContainsKey(schemaName);

    public Schema GetSchema(string schemaName)
    {
        if (!_schemaDirectory.TryGetValue(schemaName, out var schemaEntry))
        {
            throw new KeyNotFoundException($"Schema '{schemaName}' does not exist.");
        }

        return schemaEntry.Schema;
    }

    public int GetFirstDataPageNumber(string schemaName) => GetSchemaEntry(schemaName).FirstDataPageNumber;

    public int GetLastDataPageNumber(string schemaName) => GetSchemaEntry(schemaName).LastDataPageNumber;
    
    public async Task UpdateLastDataPageNumberAsync(string schemaName, int pageNumber)
    {
        var schemaEntry = GetSchemaEntry(schemaName);
        using var schemaPageLease = await pageBufferManager.LeasePageAsync<SchemaPage>(schemaEntry.SchemaPageNumber);
        var schemaPage = schemaPageLease.Page;

        schemaPage.LastDataPageNumber  = pageNumber;
        schemaEntry.LastDataPageNumber = pageNumber;
        
        pageBufferManager.MarkPageDirty(schemaEntry.SchemaPageNumber);
    }

    public async Task AddSchemaAsync(Schema schema)
    {
        if (HasSchema(schema.Name))
        {
            throw new InvalidOperationException($"Schema '{schema.Name}' already exists.");
        }

        using var headerPageLease = await pageBufferManager.LeasePageAsync<DatabaseHeaderPage>(0);
        var headerPage = headerPageLease.Page;

        // get schema directory page
        using var schemaDirectoryPageLease =
            await pageBufferManager.LeasePageAsync<SchemaDirectoryPage>(headerPage.SchemaDirectoryPageNumber);
        var schemaDirectoryPage = schemaDirectoryPageLease.Page;

        var schemaPageNumber = headerPage.PageCount++;
        // allocate a new schema page from page buffer
        using var schemaPageLease = pageBufferManager.AllocatePageLease<SchemaPage>(schemaPageNumber);
        var schemaPage = schemaPageLease.Page;

        // todo: validate name as valid
        // add info schema object to page
        schemaPage.Name = schema.Name;

        foreach (var schemaField in schema.Fields)
        {
            // todo: map better
            var type = schemaField.Type switch
            {
                SchemaFieldType.Boolean => SchemaPageFieldType.Boolean,
                SchemaFieldType.Integer => SchemaPageFieldType.Integer,
                SchemaFieldType.Long => SchemaPageFieldType.Long,
                _ => SchemaPageFieldType.String
            };
            var length = schemaField.Type switch
            {
                SchemaFieldType.Boolean => 1,
                SchemaFieldType.Integer => 4,
                SchemaFieldType.Long => 8,
                _ => schemaField.Length
            };
            schemaPage.AddField(schemaField.Name, type, (byte) length);
        }

        // update schema directory page with new schema location
        schemaDirectoryPage.InsertSchemaDirectoryEntry(schemaPageNumber);

        var newDataPageNumber = headerPage.PageCount++;
        using var newPageLease = pageBufferManager.AllocatePageLease<DataPage>(newDataPageNumber);
        var newDataPage = newPageLease.Page;
        schemaPage.FirstDataPageNumber = newDataPageNumber;
        schemaPage.LastDataPageNumber  = newDataPageNumber;

        _schemaDirectory.Add(schema.Name, new SchemaEntry(
            GetSchemaFromPage(schemaPage),
            schemaPageNumber,
            newDataPageNumber,
            newDataPageNumber));
        
        pageBufferManager.MarkPageDirty(0);
        pageBufferManager.MarkPageDirty(headerPage.SchemaDirectoryPageNumber);
        pageBufferManager.MarkPageDirty(schemaPageNumber);
        pageBufferManager.MarkPageDirty(newDataPageNumber);
    }

    public async Task RemoveSchemaAsync(string schemaName)
    {
        if (!_schemaDirectory.TryGetValue(schemaName, out var schemaEntry))
        {
            throw new KeyNotFoundException($"Schema '{schemaName}' does not exist.");
        }

        using var headerPageLease = await pageBufferManager.LeasePageAsync<DatabaseHeaderPage>(0);
        var headerPage = headerPageLease.Page;
        using var schemaDirectoryPageLease =
            pageBufferManager.AllocatePageLease<SchemaDirectoryPage>(headerPageLease.Page.SchemaDirectoryPageNumber);
        var schemaDirectoryPage = schemaDirectoryPageLease.Page;
        var directoryEntry = Array.IndexOf(schemaDirectoryPage.SchemaPageNumbers, schemaEntry.SchemaPageNumber);

        if (directoryEntry < 0)
        {
            throw new InvalidDataException(
                $"Schema '{schemaName}' points to page {schemaEntry.SchemaPageNumber}, but that page is missing from the schema directory.");
        }

        schemaDirectoryPage.ClearSchemaDirectoryEntry(directoryEntry);
        _schemaDirectory.Remove(schemaName);
        pageBufferManager.MarkPageDirty(headerPage.SchemaDirectoryPageNumber);
        pageBufferManager.MarkPageDirty(schemaEntry.SchemaPageNumber);
    }

    public bool ValidateDataAgainstSchema(Schema schema, KeyValuePair<string, object>[] data)
    {
        if (data.Length != schema.Fields.Count)
        {
            return false;
        }

        var schemaFields = new Dictionary<string, Field>(NameComparer);
        foreach (var field in schema.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name) || !schemaFields.TryAdd(field.Name, field))
            {
                return false;
            }
        }

        var suppliedFields = new HashSet<string>(NameComparer);

        foreach (var (fieldName, value) in data)
        {
            if (string.IsNullOrWhiteSpace(fieldName) ||
                !suppliedFields.Add(fieldName) ||
                !schemaFields.TryGetValue(fieldName, out var field) ||
                !ValueMatchesField(field, value))
            {
                return false;
            }
        }

        return true;
    }

    // Validate that columns provided match what's available in schema
    // Data will later be validated row by row
    // todo: split for inserts vs selects 
    public bool ValidateColumnsAgainstSchema(Schema schema, string[] columns)
    {
        var schemaColumns = schema.Fields
            .Select(field => field.Name)
            .ToHashSet(NameComparer);

        return columns.All(schemaColumns.Contains);
    }

    public bool ValidateFilterAgainstSchema(Schema schema, QueryFilter[]? filter)
    {
        if (filter is null) return true;

        var fieldsByName = schema.Fields.ToDictionary(field => field.Name, NameComparer);

        foreach (var filterPredicate in filter)
        {
            if (!fieldsByName.TryGetValue(filterPredicate.Column, out var field) ||
                !FilterValueMatchesFieldType(field.Type, filterPredicate.Value) ||
                !FilterOperatorIsSupported(field.Type, filterPredicate.Operator))
            {
                return false;
            }
        }

        return true;
    }

    private static Schema GetSchemaFromPage(SchemaPage schemaPage)
    {
        var schema = new Schema(schemaPage.Name);

        foreach (var pageField in schemaPage.Fields)
        {
            var fieldType = pageField.Type switch
            {
                SchemaPageFieldType.Boolean => SchemaFieldType.Boolean,
                SchemaPageFieldType.Integer => SchemaFieldType.Integer,
                SchemaPageFieldType.Long => SchemaFieldType.Long,
                SchemaPageFieldType.String => SchemaFieldType.String,
                _ => throw new InvalidDataException(
                    $"Schema '{schemaPage.Name}' contains an unknown field type value: {(byte) pageField.Type}.")
            };

            schema.AddField(pageField.Name, fieldType, checked((byte) pageField.Length));
        }

        return schema;
    }

    private static bool ValueMatchesField(Field field, object value)
    {
        return field.Type switch
        {
            SchemaFieldType.String => value is string stringValue &&
                                      Encoding.UTF8.GetByteCount(stringValue) <= field.Length,
            SchemaFieldType.Integer => value is int,
            SchemaFieldType.Long => value is long,
            SchemaFieldType.Boolean => value is bool,
            _ => false
        };
    }

    private static Dictionary<string, SchemaEntry> LoadSchemaDirectory(PageBufferManager pageBufferManager)
    {
        var schemas = new Dictionary<string, SchemaEntry>(NameComparer);
        using var headerPageLease = pageBufferManager.LeasePageAsync<DatabaseHeaderPage>(0).GetAwaiter().GetResult();
        var headerPage = headerPageLease.Page;
        using var schemaDirectoryPageLease = pageBufferManager
            .LeasePageAsync<SchemaDirectoryPage>(headerPage.SchemaDirectoryPageNumber)
            .GetAwaiter()
            .GetResult();
        var schemaDirectoryPage = schemaDirectoryPageLease.Page;

        foreach (var schemaPageNumber in schemaDirectoryPage.NonDeletedSchemaPageNumbers)
        {
            using var schemaPageLease = pageBufferManager.LeasePageAsync<SchemaPage>(schemaPageNumber)
                .GetAwaiter()
                .GetResult();
            var schemaPage = schemaPageLease.Page;

            if (!schemas.TryAdd(schemaPage.Name, new SchemaEntry(
                    GetSchemaFromPage(schemaPage),
                    schemaPageNumber,
                    schemaPage.FirstDataPageNumber,
                    schemaPage.LastDataPageNumber)))
            {
                throw new InvalidDataException(
                    $"The schema directory contains duplicate schema name '{schemaPage.Name}'.");
            }
        }

        return schemas;
    }

    private static bool FilterValueMatchesFieldType(SchemaFieldType fieldType, object value)
    {
        return fieldType switch
        {
            SchemaFieldType.Integer => value is int,
            SchemaFieldType.Boolean => value is bool,
            SchemaFieldType.Long => value is long,
            SchemaFieldType.String => value is string,
            _ => false
        };
    }

    private static bool FilterOperatorIsSupported(
        SchemaFieldType fieldType,
        QueryFilterOperator filterOperator)
    {
        if (!Enum.IsDefined(filterOperator))
        {
            return false;
        }

        return fieldType != SchemaFieldType.Boolean ||
               filterOperator is QueryFilterOperator.EqualTo or QueryFilterOperator.NotEqualTo;
    }

    private SchemaEntry GetSchemaEntry(string schemaName)
    {
        if (!_schemaDirectory.TryGetValue(schemaName, out var schemaEntry))
        {
            throw new KeyNotFoundException($"Schema '{schemaName}' does not exist.");
        }

        return schemaEntry;
    }

    private sealed class SchemaEntry(
        Schema schema,
        int schemaPageNumber,
        int firstDataPageNumber,
        int lastDataPageNumber)
    {
        public Schema Schema { get; } = schema;
        public int SchemaPageNumber { get; } = schemaPageNumber;
        public int FirstDataPageNumber { get; } = firstDataPageNumber;
        public int LastDataPageNumber { get; set; } = lastDataPageNumber;
    }
}