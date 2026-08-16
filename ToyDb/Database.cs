using ToyDb.Pages;

namespace ToyDb;

using System.Buffers.Binary;
using System.Text;

public class Database : IDisposable
{
    private const int EngineVersion = 2;

    private const int SchemaDirectoryPageNumber = 1;

    public DatabaseInfo Info { get; set; }

    private readonly PageBufferManager _pageBufferManager;

    private readonly SchemaManager _schemaManager;

    /*
     * Init todo list:
     * Start up page buffer
     * Grab header + info
     * Confirm database is minimally valid
     *
     * return initialized database object
     */
    private Database(string filePath)
    {
        _pageBufferManager = new PageBufferManager(
            new FileIoManager(filePath),
            pageBufferConfig: new PageBufferConfig(FrameCount: 2_000));
        var headerPage = _pageBufferManager.ReadPageAsync<DatabaseHeaderPage>(0).Result;
        var welcomeValid = headerPage.WelcomeMessage == Constants.WelcomeMessage;
        if (!welcomeValid) throw new Exception("Invalid database format.");

        _schemaManager = new SchemaManager(_pageBufferManager);

        Info = new DatabaseInfo
        {
            Version                   = headerPage.Version,
            PageCount                 = headerPage.PageCount,
            SchemaDirectoryPageNumber = headerPage.SchemaDirectoryPageNumber
        };
    }

    public static async Task InitializeAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            throw new Exception("The file already exists. Try using Open()");
        }

        using var pageBuffer = new PageBufferManager(new FileIoManager(filePath),
            pageBufferConfig: new PageBufferConfig(FrameCount: 20)); // only need a small buffer to init db

        var newHeaderPage = pageBuffer
            .AllocatePage<DatabaseHeaderPage>(0);
        newHeaderPage.Version = EngineVersion;

        pageBuffer.AllocatePage<SchemaDirectoryPage>(SchemaDirectoryPageNumber);
        newHeaderPage.SchemaDirectoryPageNumber = SchemaDirectoryPageNumber;
        newHeaderPage.PageCount                 = 2;

        await pageBuffer.FlushAsync();
    }

    public static Database Open(string filePath) =>
        !File.Exists(filePath)
            ? throw new Exception("File not found.")
            : new Database(filePath);

    public Task CloseAsync() => _pageBufferManager.FlushAsync();

    public void Dispose()
    {
        _pageBufferManager.Dispose();
    }

    public Task AddSchemaAsync(Schema schema)
    {
        return _schemaManager.AddSchemaAsync(schema);
    }

    public async Task<int> InsertAsync(string tableName, string[] columns, object[][] valueSets)
    {
        var insertedRowCount = 0;
        if (!_schemaManager.HasSchema(tableName))
        {
            throw new Exception($"Table {tableName} does not exist.");
        }

        var schemaPage = await _schemaManager.GetSchemaAsync(tableName);
        if (!_schemaManager.ValidateColumnsAgainstSchema(schemaPage, columns))
        {
            throw new Exception("Invalid columns provided");
        }

        var insertPage = await _pageBufferManager.ReadPageAsync<DataPage>(schemaPage.LastDataPageNumber);
        foreach (var valueSet in valueSets)
        {
            if (!TryValueSetValidation(schemaPage, columns, valueSet, out var errorMessage))
            {
                throw new Exception(errorMessage);
            }

            var rowData = ConvertDataToBytes(schemaPage, columns, valueSet);
            if (!HasFreeSpaceForInsert(insertPage, rowData.Length))
            {
                var headerPage = await _pageBufferManager.ReadPageAsync<DatabaseHeaderPage>(0);
                var insertedPageNumber = ++headerPage.PageCount;
                var newDataPage = _pageBufferManager.AllocatePage<DataPage>(insertedPageNumber);
                insertPage.OverFlowPageNumber = insertedPageNumber;
                insertPage                    = newDataPage;
                schemaPage.LastDataPageNumber = insertedPageNumber;
            }

            insertPage.InsertData(rowData);
            insertedRowCount++;
        }

        return insertedRowCount;
    }

    public async IAsyncEnumerable<object[]> SelectAsync(
        string tableName,
        string[] columns,
        QueryFilter[]? filter = null)
    {
        if (!_schemaManager.HasSchema(tableName))
        {
            throw new Exception($"Table {tableName} does not exist.");
        }

        var schemaPage = await _schemaManager.GetSchemaAsync(tableName);
        if (!_schemaManager.ValidateColumnsAgainstSchema(schemaPage, columns))
        {
            throw new Exception("Invalid columns provided");
        }

        if (filter is not null && !_schemaManager.ValidateFilterAgainstSchema(schemaPage, filter))
        {
            throw new Exception("Invalid filter provided");
        }

        var dataPageNumber = schemaPage.FirstDataPageNumber;
        do
        {
            var dataPage = await _pageBufferManager.ReadPageAsync<DataPage>(dataPageNumber);
            dataPageNumber = dataPage.OverFlowPageNumber;

            foreach (var slot in dataPage.EnumerateSlots())
            {
                if (!slot.InUse) continue;

                var dataRow = dataPage.Data.Slice(slot.OffsetStart, slot.Length);

                if (DataRowPassesFilter(schemaPage, dataRow, filter))
                {
                    yield return columns.Select(column => GetData(schemaPage, dataRow, column)).ToArray();
                }
            }
        } while (dataPageNumber != -1);
    }

    private bool DataRowPassesFilter(SchemaPage schemaPage, Memory<byte> dataRow, QueryFilter[]? filter)
    {
        if (filter is null) return true;

        return filter.All(filterPredicate =>
        {
            var columnData = GetData(schemaPage, dataRow, filterPredicate.Column);
            return CompareValues(columnData,
                schemaPage.Fields.First(x => x.Name == filterPredicate.Column).Type,
                filterPredicate.Operator,
                filterPredicate.Value);
        });
    }

    // Contract: Assume valid data at this point
    // Compare (columnData) of (type) with against (filterPredicateValue) using (operator)
    // return true or false depending on match 
    private static bool CompareValues(
        object columnData,
        SchemaPageFieldType type,
        QueryFilterOperator filterPredicateOperator,
        object filterPredicateValue)
    {
        var comparison = type switch
        {
            SchemaPageFieldType.Integer => ((int) columnData).CompareTo((int) filterPredicateValue),
            SchemaPageFieldType.Boolean => ((bool) columnData).CompareTo((bool) filterPredicateValue),
            SchemaPageFieldType.Long => ((long) columnData).CompareTo((long) filterPredicateValue),
            SchemaPageFieldType.String => StringComparer.Ordinal.Compare(
                (string) columnData,
                (string) filterPredicateValue),
            _ => throw new InvalidDataException($"Unknown schema field type: {type}.")
        };

        return filterPredicateOperator switch
        {
            QueryFilterOperator.LessThan => comparison < 0,
            QueryFilterOperator.GreaterThan => comparison > 0,
            QueryFilterOperator.LessThanOrEqualTo => comparison <= 0,
            QueryFilterOperator.GreaterThanOrEqualTo => comparison >= 0,
            QueryFilterOperator.EqualTo => comparison == 0,
            QueryFilterOperator.NotEqualTo => comparison != 0,
            _ => throw new ArgumentOutOfRangeException(
                nameof(filterPredicateOperator),
                filterPredicateOperator,
                "Unknown query filter operator.")
        };
    }

    private object GetData(SchemaPage schemaPage, Memory<byte> dataRow, string column)
    {
        // todo: GetData evaluates schemaPage.Fields and performs a linear name search for every field of every row.
        // Resolve the requested SchemaPageField objects once before scanning.
        var schemaColumn = schemaPage.Fields.First(x => x.Name == column);

        var data = dataRow.Slice(schemaColumn.Offset, schemaColumn.Length).Span;

        return schemaColumn.Type switch
        {
            SchemaPageFieldType.Integer => BinaryPrimitives.ReadInt32LittleEndian(data),
            SchemaPageFieldType.Boolean => BitConverter.ToBoolean(data),
            SchemaPageFieldType.Long => BinaryPrimitives.ReadInt64LittleEndian(data),
            SchemaPageFieldType.String => Encoding.UTF8.GetString(data).TrimEnd('\0'),
            _ => throw new Exception("unknown type")
        };
    }

    private ReadOnlyMemory<byte> ConvertDataToBytes(SchemaPage schemaPage, string[] columns, object[] valueSet)
    {
        var fields = schemaPage.Fields;
        var fieldsByName = fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var rowData = new byte[fields.Sum(field => field.Length)];

        for (var i = 0; i < columns.Length; i++)
        {
            var field = fieldsByName[columns[i]];
            var destination = rowData.AsSpan(field.Offset, field.Length);

            switch (field.Type)
            {
                case SchemaPageFieldType.Integer:
                    BinaryPrimitives.WriteInt32LittleEndian(destination, (int) valueSet[i]);
                    break;
                case SchemaPageFieldType.Boolean:
                    destination[0] = (bool) valueSet[i] ? (byte) 1 : (byte) 0;
                    break;
                case SchemaPageFieldType.Long:
                    BinaryPrimitives.WriteInt64LittleEndian(destination, (long) valueSet[i]);
                    break;
                case SchemaPageFieldType.String:
                    Encoding.UTF8.GetBytes((string) valueSet[i], destination);
                    break;
                default:
                    throw new InvalidDataException(
                        $"Column '{field.Name}' has an unknown field type value: {(byte) field.Type}.");
            }
        }

        return rowData;
    }

    private static bool HasFreeSpaceForInsert(DataPage dataPage, int rowLength)
    {
        return rowLength >= 0 && rowLength + DataPage.SlotSize <= dataPage.FreeSpaceSize;
    }

    private static bool TryValueSetValidation(
        SchemaPage schemaPage,
        string[] columns,
        object[] valueSet,
        out string errorMessage)
    {
        if (valueSet.Length != columns.Length)
        {
            errorMessage = $"Expected {columns.Length} values, but received {valueSet.Length}.";
            return false;
        }

        var fieldsByName = schemaPage.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);

        for (var i = 0; i < columns.Length; i++)
        {
            if (!fieldsByName.TryGetValue(columns[i], out var field))
            {
                errorMessage = $"Column '{columns[i]}' does not exist in schema '{schemaPage.Name}'.";
                return false;
            }

            var value = valueSet[i];
            if (value is null)
            {
                errorMessage = $"Column '{field.Name}' does not accept null values.";
                return false;
            }

            var valueIsValid = field.Type switch
            {
                SchemaPageFieldType.Integer => field.Length == sizeof(int) && value is int,
                SchemaPageFieldType.Boolean => field.Length == sizeof(byte) && value is bool,
                SchemaPageFieldType.Long => field.Length == sizeof(long) && value is long,
                SchemaPageFieldType.String => value is string stringValue &&
                                              Encoding.UTF8.GetByteCount(stringValue) <= field.Length,
                _ => false
            };

            if (!valueIsValid)
            {
                errorMessage = $"Value for column '{field.Name}' does not match its {field.Type} definition.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }
}

public record QueryFilter(string Column, QueryFilterOperator Operator, object Value);

public enum QueryFilterOperator
{
    LessThan,
    GreaterThan,
    LessThanOrEqualTo,
    GreaterThanOrEqualTo,
    EqualTo,
    NotEqualTo
}

public record DatabaseInfo
{
    public int Version { get; init; }
    public int PageCount { get; init; }
    public int SchemaDirectoryPageNumber { get; init; }
}
