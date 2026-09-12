using ToyDb.Pages;

namespace ToyDb;

public class Database : IDisposable
{
    private const int EngineVersion = 2;

    private const int SchemaDirectoryPageNumber = 1;

    static Database()
    {
        if (!BitConverter.IsLittleEndian)
            throw new PlatformNotSupportedException("ToyDb requires a little-endian platform.");
    }

    public DatabaseInfo Info { get; set; }

    private readonly PageBufferManager _pageBufferManager;

    private readonly SchemaManager _schemaManager;

    private readonly ExecutionEngine _executionEngine;


    /*
     * Init procedure:
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
            pageBufferConfig: new PageBufferConfig(FrameCount: 2000));
        using var headerPageLease = _pageBufferManager.LeasePageAsync<DatabaseHeaderPage>(0).Result;
        var headerPage = headerPageLease.Page;
        var welcomeValid = headerPage.WelcomeMessage == Constants.WelcomeMessage;
        if (!welcomeValid) throw new Exception("Invalid database format.");

        _schemaManager   = new SchemaManager(_pageBufferManager);
        var databaseManager = new DatabaseManager(_pageBufferManager);
        _executionEngine = new ExecutionEngine(_pageBufferManager, _schemaManager, databaseManager);

        Info = new DatabaseInfo
        {
            Version                   = headerPage.Version,
            PageCount                 = headerPage.PageCount,
            SchemaDirectoryPageNumber = headerPage.SchemaDirectoryPageNumber,
            FilePath = filePath
        };
    }

    private static async Task<Database> InitializeAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            throw new Exception("The file already exists. Try using Open()");
        }

        using var pageBuffer = new PageBufferManager(new FileIoManager(filePath),
            pageBufferConfig: new PageBufferConfig(FrameCount: 20)); // only need a small buffer to init db

        using (var newHeaderPageLease = pageBuffer
                   .AllocatePageLease<DatabaseHeaderPage>(0))
        {
            var newHeaderPage = newHeaderPageLease.Page;
            newHeaderPage.Version = EngineVersion;
            using (pageBuffer.AllocatePageLease<SchemaDirectoryPage>(SchemaDirectoryPageNumber))
            {
                newHeaderPage.SchemaDirectoryPageNumber = SchemaDirectoryPageNumber;
                newHeaderPage.PageCount                 = 2;
            }
        }
        await pageBuffer.FlushAsync();
        return await OpenAsync(filePath);
    }

    public static Task<Database> OpenAsync(string filePath) =>
        !File.Exists(filePath)
            ? InitializeAsync(filePath)
            : Task.FromResult(new Database(filePath));

    public async Task CloseAsync()
    {
        await _pageBufferManager.FlushAsync();
        _pageBufferManager.Dispose();
    }

    public void Dispose()
    {
        _pageBufferManager.FlushAsync().GetAwaiter().GetResult();
        _pageBufferManager.Dispose();
    }

    public Task AddSchemaAsync(Schema schema)
    {
        return _schemaManager.AddSchemaAsync(schema);
    }

    public Task RemoveSchemaAsync(string schemaName)
    {
        return _schemaManager.RemoveSchemaAsync(schemaName);
    }
    
    public Schema GetSchema(string schemaName)
    {
        return _schemaManager.GetSchema(schemaName);
    }

    public Task<int> InsertAsync(string tableName, string[] columns, IEnumerable<object[]> valueSets)
    {
        return _executionEngine.InsertAsync(tableName, columns, valueSets);
    }

    public Task<int> DeleteAsync(
        string tableName,
        QueryFilter[]? filter = null)
    {
        return _executionEngine.DeleteAsync(tableName, filter);
    }

    public IAsyncEnumerable<object[]> SelectAsync(
        string tableName,
        string[] columns,
        QueryFilter[]? filter = null)
    {
        return _executionEngine.SelectAsync(tableName, columns, filter);
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
    public string FilePath { get; init; }
}