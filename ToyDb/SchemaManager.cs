using ToyDb.Pages;

namespace ToyDb;

using System.Text;

public class SchemaManager(PageBufferManager pageBufferManager)
{
    private static readonly StringComparer NameComparer = StringComparer.Ordinal;

    private readonly Dictionary<string, int> _schemaDirectory = LoadSchemaDirectory(pageBufferManager);

    public bool HasSchema(string schemaName) => _schemaDirectory.ContainsKey(schemaName);

    public Task<SchemaPage> GetSchemaAsync(string schemaName)
    {
        if (!_schemaDirectory.TryGetValue(schemaName, out var schemaPageNumber))
        {
            throw new KeyNotFoundException($"Schema '{schemaName}' does not exist.");
        }

        return pageBufferManager.ReadPageAsync<SchemaPage>(schemaPageNumber);
    }

    public async Task AddSchemaAsync(Schema schema)
    {
        if (HasSchema(schema.Name))
        {
            throw new InvalidOperationException($"Schema '{schema.Name}' already exists.");
        }

        var headerPage = await pageBufferManager.ReadPageAsync<DatabaseHeaderPage>(0);

        // get schema directory page
        var schemaDirectoryPage =
            await pageBufferManager.ReadPageAsync<SchemaDirectoryPage>(headerPage.SchemaDirectoryPageNumber);

        var schemaPageNumber = ++headerPage.PageCount;
        // allocate a new schema page from page buffer
        var schemaPage = pageBufferManager.AllocatePage<SchemaPage>(schemaPageNumber);

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

        var newDataPageNumber = ++headerPage.PageCount;
        pageBufferManager.AllocatePage<DataPage>(newDataPageNumber);
        schemaPage.FirstDataPageNumber = newDataPageNumber;
        schemaPage.LastDataPageNumber  = newDataPageNumber;

        _schemaDirectory.Add(schema.Name, schemaPageNumber);
    }

    public async Task RemoveSchemaAsync(string schemaName)
    {
        if (!_schemaDirectory.TryGetValue(schemaName, out var schemaPageNumber))
        {
            throw new KeyNotFoundException($"Schema '{schemaName}' does not exist.");
        }

        var headerPage = await pageBufferManager.ReadPageAsync<DatabaseHeaderPage>(0);
        var schemaDirectoryPage =
            await pageBufferManager.ReadPageAsync<SchemaDirectoryPage>(headerPage.SchemaDirectoryPageNumber);
        var directoryEntry = Array.IndexOf(schemaDirectoryPage.SchemaPageNumbers, schemaPageNumber);

        if (directoryEntry < 0)
        {
            throw new InvalidDataException(
                $"Schema '{schemaName}' points to page {schemaPageNumber}, but that page is missing from the schema directory.");
        }

        schemaDirectoryPage.ClearSchemaDirectoryEntry(directoryEntry);
        _schemaDirectory.Remove(schemaName);
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

    // Validate that columns provided match what's available in schema
    // Data will later be validated row by row
    // todo: split for inserts vs selects 
    public bool ValidateColumnsAgainstSchema(SchemaPage schema, string[] columns)
    {
        var schemaColumns = schema.Fields
            .Select(field => field.Name)
            .ToHashSet(NameComparer);

        return columns.All(schemaColumns.Contains);
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

    private static Dictionary<string, int> LoadSchemaDirectory(PageBufferManager pageBufferManager)
    {
        var schemas = new Dictionary<string, int>(NameComparer);
        var headerPage = pageBufferManager.ReadPageAsync<DatabaseHeaderPage>(0).GetAwaiter().GetResult();
        var schemaDirectoryPage = pageBufferManager
            .ReadPageAsync<SchemaDirectoryPage>(headerPage.SchemaDirectoryPageNumber)
            .GetAwaiter()
            .GetResult();

        foreach (var schemaPageNumber in schemaDirectoryPage.NonDeletedSchemaPageNumbers)
        {
            var schemaPage = pageBufferManager.ReadPageAsync<SchemaPage>(schemaPageNumber)
                .GetAwaiter()
                .GetResult();

            if (!schemas.TryAdd(schemaPage.Name, schemaPageNumber))
            {
                throw new InvalidDataException(
                    $"The schema directory contains duplicate schema name '{schemaPage.Name}'.");
            }
        }

        return schemas;
    }

    public bool ValidateFilterAgainstSchema(SchemaPage schemaPage, QueryFilter[]? filter)
    {
        if (filter is null)
        {
            return true;
        }

        var fieldsByName = schemaPage.Fields.ToDictionary(field => field.Name, NameComparer);

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

    private static bool FilterValueMatchesFieldType(SchemaPageFieldType fieldType, object value)
    {
        return fieldType switch
        {
            SchemaPageFieldType.Integer => value is int,
            SchemaPageFieldType.Boolean => value is bool,
            SchemaPageFieldType.Long => value is long,
            SchemaPageFieldType.String => value is string,
            _ => false
        };
    }

    private static bool FilterOperatorIsSupported(
        SchemaPageFieldType fieldType,
        QueryFilterOperator filterOperator)
    {
        if (!Enum.IsDefined(filterOperator))
        {
            return false;
        }

        return fieldType != SchemaPageFieldType.Boolean ||
               filterOperator is QueryFilterOperator.EqualTo or QueryFilterOperator.NotEqualTo;
    }
}
