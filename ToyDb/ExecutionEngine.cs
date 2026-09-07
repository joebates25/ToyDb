using System.Buffers.Binary;
using System.Text;
using ToyDb.AST;
using ToyDb.Pages;

namespace ToyDb;

public class ExecutionEngine(PageBufferManager pageBufferManager, SchemaManager schemaManager)
{
    public async Task<int> InsertAsync(string tableName, string[] columns, object[][] valueSets)
    {
        var insertedRowCount = 0;
        if (!schemaManager.HasSchema(tableName))
        {
            throw new Exception($"Table {tableName} does not exist.");
        }

        var schema = schemaManager.GetSchema(tableName);
        if (!schemaManager.ValidateColumnsAgainstSchema(schema, columns))
        {
            throw new Exception("Invalid columns provided");
        }

        using var insertPageLease = await pageBufferManager.LeasePageAsync<DataPage>(
            schemaManager.GetLastDataPageNumber(tableName));
        insertPageLease.MarkDirty();
        var insertPage = insertPageLease.Page;
        foreach (var valueSet in valueSets)
        {
            if (!TryValueSetValidation(schema, columns, valueSet, out var errorMessage))
            {
                throw new Exception(errorMessage);
            }

            var rowData = ConvertDataToBytes(schema, columns, valueSet);
            if (!HasFreeSpaceForInsert(insertPage, rowData.Length))
            {
                using var headerPageLease = await pageBufferManager.LeasePageAsync<DatabaseHeaderPage>(0);
                headerPageLease.MarkDirty();
                var headerPage = headerPageLease.Page;
                var insertedPageNumber = ++headerPage.PageCount;
                using var newDataPageLease = pageBufferManager.AllocatePageLease<DataPage>(insertedPageNumber);
                insertPage.OverFlowPageNumber = insertedPageNumber;
                insertPage                    = newDataPageLease.Page;
                await schemaManager.UpdateLastDataPageNumberAsync(tableName, insertedPageNumber);
            }

            insertPage.InsertCell(rowData);
            insertedRowCount++;
        }

        return insertedRowCount;
    }

    public IAsyncEnumerable<object[]> SelectAsync(
        SelectExpression selectExpression)
    {
        return SelectAsync(
            selectExpression.TableName,
            selectExpression.Columns.ToArray(),
            selectExpression.WhereExpressions?.Select(where => new QueryFilter(
                where.ColumnName,
                where.Operator.Operator switch
                {
                    ">" => QueryFilterOperator.GreaterThan,
                    "<" => QueryFilterOperator.LessThan,
                    "=" => QueryFilterOperator.EqualTo,
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(where.Operator),
                        where.Operator,
                        "Unknown query filter operator.")
                },
                where.Value)).ToArray());
    }

    public async IAsyncEnumerable<object[]> SelectAsync(
        string tableName,
        string[] columns,
        QueryFilter[]? filter = null)
    {
        if (!schemaManager.HasSchema(tableName))
        {
            throw new Exception($"Table {tableName} does not exist.");
        }

        var schema = schemaManager.GetSchema(tableName);
        if (!schemaManager.ValidateColumnsAgainstSchema(schema, columns))
        {
            throw new Exception("Invalid columns provided");
        }

        if (filter is not null && !schemaManager.ValidateFilterAgainstSchema(schema, filter))
        {
            throw new Exception("Invalid filter provided");
        }

        var dataPageNumber = schemaManager.GetFirstDataPageNumber(tableName);
        do
        {
            using var dataPageLease = (await pageBufferManager.LeasePageAsync<DataPage>(dataPageNumber));
            var dataPage = dataPageLease.Page;
            dataPageNumber = dataPage.OverFlowPageNumber;

            foreach (var slot in dataPage.Slots())
            {
                if (!slot.InUse) continue;

                var dataRow = dataPage.Data.Slice(slot.OffsetStart, slot.Length);

                if (DataRowPassesFilter(schema, dataRow, filter))
                {
                    yield return columns.Select(column => GetData(schema, dataRow, column)).ToArray();
                }
            }
        } while (dataPageNumber != -1);
    }

    public async Task<int> DeleteAsync(
        string tableName,
        QueryFilter[]? filter = null)
    {
        if (!schemaManager.HasSchema(tableName))
        {
            throw new Exception($"Table {tableName} does not exist.");
        }

        var schema = schemaManager.GetSchema(tableName);

        if (filter is not null && !schemaManager.ValidateFilterAgainstSchema(schema, filter))
        {
            throw new Exception("Invalid filter provided");
        }

        var dataPageNumber = schemaManager.GetFirstDataPageNumber(tableName);
        var deleteCount = 0;
        do
        {
            using var dataPageLease = await pageBufferManager.LeasePageAsync<DataPage>(dataPageNumber);

            var dataPage = dataPageLease.Page;
            dataPageNumber = dataPage.OverFlowPageNumber;

            for (var slotNum = 0; slotNum < dataPage.SlotCount; slotNum++)
            {
                var slot = dataPage.GetSlot(slotNum);
                if (!slot.InUse) continue;

                var dataRow = dataPage.Data.Slice(slot.OffsetStart, slot.Length);

                if (!DataRowPassesFilter(schema, dataRow, filter)) continue;

                dataPage.FreeSlot(slotNum);
                dataPageLease.MarkDirty();
                deleteCount++;
            }
        } while (dataPageNumber != -1);

        return deleteCount;
    }

    private bool DataRowPassesFilter(Schema schema, Memory<byte> dataRow, QueryFilter[]? filter)
    {
        if (filter is null) return true;

        return filter.All(filterPredicate =>
        {
            var columnData = GetData(schema, dataRow, filterPredicate.Column);
            return CompareValues(columnData,
                schema.Fields.First(x => x.Name == filterPredicate.Column).Type,
                filterPredicate.Operator,
                filterPredicate.Value);
        });
    }

    // Contract: Assume valid data at this point
    // Compare (columnData) of (type) with against (filterPredicateValue) using (operator)
    // return true or false depending on match 
    private static bool CompareValues(
        object columnData,
        SchemaFieldType type,
        QueryFilterOperator filterPredicateOperator,
        object filterPredicateValue)
    {
        var comparison = type switch
        {
            SchemaFieldType.Integer => ((int) columnData).CompareTo((int) filterPredicateValue),
            SchemaFieldType.Boolean => ((bool) columnData).CompareTo((bool) filterPredicateValue),
            SchemaFieldType.Long => ((long) columnData).CompareTo((long) filterPredicateValue),
            SchemaFieldType.String => StringComparer.Ordinal.Compare(
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

    private object GetData(Schema schema, Memory<byte> dataRow, string column)
    {
        // todo: GetData evaluates schema.Fields and performs a linear name search for every field of every row.
        // Resolve the requested Field objects once before scanning.
        var schemaColumn = schema.Fields.First(x => x.Name == column);

        var data = dataRow.Slice(schemaColumn.Offset, schemaColumn.Length).Span;

        return schemaColumn.Type switch
        {
            SchemaFieldType.Integer => BinaryPrimitives.ReadInt32LittleEndian(data),
            SchemaFieldType.Boolean => BitConverter.ToBoolean(data),
            SchemaFieldType.Long => BinaryPrimitives.ReadInt64LittleEndian(data),
            SchemaFieldType.String => Encoding.UTF8.GetString(data).TrimEnd('\0'),
            _ => throw new Exception("unknown type")
        };
    }

    private ReadOnlyMemory<byte> ConvertDataToBytes(Schema schema, string[] columns, object[] valueSet)
    {
        var fields = schema.Fields;
        var fieldsByName = fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var rowData = new byte[fields.Sum(field => field.Length)];

        for (var i = 0; i < columns.Length; i++)
        {
            var field = fieldsByName[columns[i]];
            var destination = rowData.AsSpan(field.Offset, field.Length);

            switch (field.Type)
            {
                case SchemaFieldType.Integer:
                    BinaryPrimitives.WriteInt32LittleEndian(destination, (int) valueSet[i]);
                    break;
                case SchemaFieldType.Boolean:
                    destination[0] = (bool) valueSet[i] ? (byte) 1 : (byte) 0;
                    break;
                case SchemaFieldType.Long:
                    BinaryPrimitives.WriteInt64LittleEndian(destination, (long) valueSet[i]);
                    break;
                case SchemaFieldType.String:
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
        Schema schema,
        string[] columns,
        object[] valueSet,
        out string errorMessage)
    {
        if (valueSet.Length != columns.Length)
        {
            errorMessage = $"Expected {columns.Length} values, but received {valueSet.Length}.";
            return false;
        }

        var fieldsByName = schema.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);

        for (var i = 0; i < columns.Length; i++)
        {
            if (!fieldsByName.TryGetValue(columns[i], out var field))
            {
                errorMessage = $"Column '{columns[i]}' does not exist in schema '{schema.Name}'.";
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
                SchemaFieldType.Integer => field.Length == sizeof(int) && value is int,
                SchemaFieldType.Boolean => field.Length == sizeof(byte) && value is bool,
                SchemaFieldType.Long => field.Length == sizeof(long) && value is long,
                SchemaFieldType.String => value is string stringValue &&
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