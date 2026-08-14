using ToyDb.Pages;

namespace ToyDb;

public class Database : IDisposable
{
    private const int EngineVersion = 2;

    private const int SchemaDirectoryPageNumber = 1;

    public DatabaseInfo Info { get; set; }

    private readonly PageBufferManager _pageBufferManager;

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
        newHeaderPage.SetVersion(EngineVersion);
        newHeaderPage.SetPageCount(0); //todo: update once we get a real page directory

        pageBuffer.AllocatePage<SchemaDirectoryPage>(SchemaDirectoryPageNumber);
        newHeaderPage.SetSchemaDirectoryPageNumber(SchemaDirectoryPageNumber);

        await pageBuffer.FlushAsync();
    }

    public static Database Open(string filePath) =>
        !File.Exists(filePath)
            ? throw new Exception("File not found.")
            : new Database(filePath);

    public void Dispose() => _pageBufferManager.Dispose();

    public async Task AddSchemaAsync(Schema schema)
    {
        // get schema directory page
        var schemaDirectoryPage =
            await _pageBufferManager.ReadPageAsync<SchemaDirectoryPage>(SchemaDirectoryPageNumber);
        
        
        // allocate a new schema page from page buffer
        // todo: (but how do we know most recently free page???
        var schemaPage = _pageBufferManager.AllocatePage<SchemaPage>(2); // todo: no longer hard code to 2
        
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
        schemaDirectoryPage.InsertSchemaDirectoryEntry(2); // todo: still hard coded. dang
    }
}

public record DatabaseInfo
{
    public int Version { get; init; }
    public int PageCount { get; init; }
    public int SchemaDirectoryPageNumber { get; init; }
}