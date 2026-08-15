using ToyDb.Pages;

namespace ToyDb;

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
            if (!TryValueSetValidation(schemaPage, valueSet, out string errorMessage))
            {
                throw new Exception(errorMessage);
            }

            if (!HasFreeSpaceForInsert(schemaPage, valueSet))
            {
                var headerPage = (await _pageBufferManager.ReadPageAsync<DatabaseHeaderPage>(0));
                var insertedPageNumber = ++headerPage.PageCount;
                var newDataPage = _pageBufferManager.AllocatePage<DataPage>(insertedPageNumber);
                insertPage.OverFlowPageNumber = insertedPageNumber;
                insertPage                    = newDataPage;
            }

            var rowData = ConvertDataToBytes(schemaPage, columns, valueSet);
            insertPage.InsertData(rowData);
            insertedRowCount++;
        }

        return insertedRowCount;
    }

    private ReadOnlyMemory<byte> ConvertDataToBytes(SchemaPage schemaPage, string[] columns, object[] valueSet)
    {
        throw new NotImplementedException();
    }

    private bool HasFreeSpaceForInsert(SchemaPage schemaPage, object[] valueSet)
    {
        throw new NotImplementedException();
    }

    private bool TryValueSetValidation(SchemaPage schemaPage, object[] valueSet, out string errorMessage)
    {
        throw new NotImplementedException();
    }
}

public record DatabaseInfo
{
    public int Version { get; init; }
    public int PageCount { get; init; }
    public int SchemaDirectoryPageNumber { get; init; }
}
