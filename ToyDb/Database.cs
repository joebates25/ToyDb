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

        _schemaManager   = new SchemaManager(_pageBufferManager);
        _executionEngine = new ExecutionEngine(_pageBufferManager, _schemaManager);

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
    
    public Task RemoveSchemaAsync(string schemaName)
    {
        return _schemaManager.RemoveSchemaAsync(schemaName);
    }

    public Task<int> InsertAsync(string tableName, string[] columns, object[][] valueSets)
    {
        return _executionEngine.InsertAsync(tableName, columns, valueSets);
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
}
