using ToyDb.Pages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    private readonly ServiceProvider _services;

    private readonly ILogger _logger;

    private bool _disposed;


    /*
     * Init procedure:
     * Start up page buffer
     * Grab header + info
     * Confirm database is minimally valid
     *
     * return initialized database object
     */
    private Database(string filePath, ServiceProvider services, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<Database>();
        _logger.LogInformation("Opening database {FilePath}.", filePath);
        _services = services;
        _pageBufferManager = services.GetRequiredService<PageBufferManager>();
        using var headerPageLease = _pageBufferManager.LeasePageAsync<DatabaseHeaderPage>(0).Result;
        var headerPage = headerPageLease.Page;
        var welcomeValid = headerPage.WelcomeMessage == Constants.WelcomeMessage;
        if (!welcomeValid) throw new Exception("Invalid database format.");

        _schemaManager = services.GetRequiredService<SchemaManager>();
        _executionEngine = services.GetRequiredService<ExecutionEngine>();

        Info = new DatabaseInfo
        {
            Version                   = headerPage.Version,
            PageCount                 = headerPage.PageCount,
            SchemaDirectoryPageNumber = headerPage.SchemaDirectoryPageNumber,
            FilePath = filePath
        };
    }

    private static async Task<Database> InitializeAsync(string filePath, DatabaseConfig? config = null)
    {
        if (File.Exists(filePath))
        {
            throw new Exception("The file already exists. Try using Open()");
        }

        config ??= new DatabaseConfig { FrameCount = 20 };
        using var services = DatabaseServices.Create(filePath, config);
        var pageBuffer = services.GetRequiredService<PageBufferManager>();

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

    public static Task<Database> OpenAsync(string filePath, DatabaseConfig? config = null)
    {
        if (!File.Exists(filePath)) return InitializeAsync(filePath, config);

        var services = DatabaseServices.Create(filePath, config);
        try
        {
            return Task.FromResult(new Database(filePath, services, services.GetRequiredService<ILoggerFactory>()));
        }
        catch
        {
            services.Dispose();
            throw;
        }
    }

    public async Task CloseAsync()
    {
        if (_disposed) return;

        try
        {
            _logger.LogInformation("Closing database {FilePath}.", Info.FilePath);
            await _pageBufferManager.FlushAsync();
        }
        finally
        {
            _disposed = true;
            _services.Dispose();
        }
    }

    public void Dispose()
    {
        CloseAsync().GetAwaiter().GetResult();
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

public record DatabaseConfig
{
    public int FrameCount { get; init; } = 20;
}