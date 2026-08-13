using System.Buffers.Binary;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using ToyDb.Pages;

namespace ToyDb;

public class Database : IDisposable
{
    private const int EngineVersion = 2;

    private const int SchemaDirectoryPageNumber = 1;

    public DatabaseInfo Info { get; set; }

    private readonly SafeFileHandle _safeFileHandle;

    private readonly PageBuffer _pageBuffer;

    /*
     * Init todo list:
     * Start up page buffer
     * Grab header + info
     * Confirm database is minimally valid
     *
     * return initialized database object
     */
    private Database(SafeFileHandle safeFileHandle)
    {
        _safeFileHandle = safeFileHandle;

        _pageBuffer = new PageBuffer(_safeFileHandle);
        var headerPage = _pageBuffer.ReadPageAsync(0).Result.AsDatabaseHeaderPage();
        var welcomeValid = headerPage.WelcomeMessage == Constants.WelcomeMessage;
        if (!welcomeValid) throw new Exception("Invalid database format.");

        Info = new DatabaseInfo
        {
            Version                   = headerPage.Version,
            PageDirectoryPageNumber   = headerPage.PageDirectoryPageNumber,
            SchemaDirectoryPageNumber = headerPage.SchemaDirectoryPageNumber
        };
    }

    public static async Task Initialize(string filePath)
    {
        if (File.Exists(filePath))
        {
            throw new Exception("The file already exists. Try using Open()");
        }

        var safeHandle = File.OpenHandle(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        var pageBuffer = new PageBuffer(safeHandle);

        var newHeaderPage = pageBuffer
            .AllocatePage(0)
            .AsDatabaseHeaderPage();
        newHeaderPage.SetVersion(EngineVersion);
        newHeaderPage.SetPageDirectoryPageNumber(0); //todo: update once we get a real page directory
        
        pageBuffer
            .AllocatePage(SchemaDirectoryPageNumber)
            .AsSchemaDirectoryPage();
        newHeaderPage.SetSchemaDirectoryPageNumber(SchemaDirectoryPageNumber);

        await pageBuffer.FlushAsync();
    }

    public static Database Open(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new Exception("File not found.");
        }

        var safeHandle = File.OpenHandle(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        return new Database(safeHandle);
    }

    public void Dispose()
    {
        _safeFileHandle.Dispose();
    }

    public async Task AddSchema(Schema schema)
    {
        // get schema directory page
        var schemaDirectoryPage = (await _pageBuffer.ReadPageAsync(SchemaDirectoryPageNumber))
            .AsSchemaDirectoryPage();
        // allocate a new schema page from page buffer
        // todo: (but how do we know most recently free page???
        var schemaPage = _pageBuffer.AllocatePage(2) // todo: no longer hard code to 2
            .AsSchemaPage();
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
    public int PageDirectoryPageNumber { get; init; }
    public int SchemaDirectoryPageNumber { get; init; }
}