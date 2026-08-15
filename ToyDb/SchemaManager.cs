using ToyDb.Pages;

namespace ToyDb;

public class SchemaManager(PageBufferManager pageBufferManager)
{
    private readonly Dictionary<string, int> _schemaDirectory = new();

    public bool HasSchema(string schemaName) => _schemaDirectory.ContainsKey(schemaName);

    public Task<SchemaPage> GetSchemaAsync(string schemaName)
    {
        return pageBufferManager.ReadPageAsync<SchemaPage>(_schemaDirectory[schemaName]);
    }

    public async Task AddSchemaAsync(Schema schema)
    {
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

        _schemaDirectory.Add(schema.Name, schemaPageNumber);
    }

    public async Task RemoveSchemaAsync(string schemaName)
    {
        throw new NotImplementedException();
    }

    public bool ValidateDataAgainstSchema(Schema schema, KeyValuePair<string, object>[] data)
    {
        throw new NotImplementedException();
    }

    private SchemaPage GetShemaFromPage(SchemaPage schemaPage)
    {
        throw new NotImplementedException();
    }

    // Validate that columns provided match what's available in schema
    // Data will later be validated row by row
    public bool ValidateColumnsAgainstSchema(SchemaPage schema, string[] columns)
    {
        throw new NotImplementedException();
    }
}