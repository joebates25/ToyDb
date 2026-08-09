using System.Buffers.Binary;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace ToyDb;

public class Database : IDisposable
{
    private const int EngineVersion = 2;
    public DatabaseHeader Header { get; set; }

    private readonly SafeFileHandle _safeFileHandle;

    private Database(SafeFileHandle safeFileHandle, DatabaseHeader header)
    {
        _safeFileHandle = safeFileHandle;
        Header          = header;
    }

    public static async Task<Database> Initialize(string filePath)
    {
        if (File.Exists(filePath))
        {
            throw new Exception("The file already exists. Try using Open()");
        }
        
        var safeHandle = File.OpenHandle(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        
        var pageBuffer = new PageBuffer(safeHandle);
        var newHeaderPage = pageBuffer
            .AllocatePage(0)
            .AsHeaderPage();

        newHeaderPage.SetVersion(EngineVersion);
        newHeaderPage.SetPageDirectoryPageNumber(0);
        newHeaderPage.SetTableDirectoryPageNumber(0);

        await pageBuffer.FlushAsync();

        return new Database(safeHandle, new DatabaseHeader
        {
            Version                  = EngineVersion,
            PageDirectoryPageNumber  = 0,
            TableDirectoryPageNumber = 0
        });
    }

    public static async Task<Database> OpenAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new Exception("File not found.");
        }

        var safeHandle = File.OpenHandle(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        
        var pageBuffer = new PageBuffer(safeHandle);
        var headerPage = (await pageBuffer.ReadPageAsync(0)).AsHeaderPage();
        var welcomeValid = headerPage.WelcomeMessage == Constants.WelcomeMessage;
        if (!welcomeValid) throw new Exception("Invalid database format.");

        return new Database(safeHandle,
            new DatabaseHeader
            {
                Version                  = headerPage.Version,
                PageDirectoryPageNumber  = headerPage.PageDirectoryPageNumber,
                TableDirectoryPageNumber = headerPage.TableDirectoryPageNumber
            });
    }

    public void Dispose()
    {
        _safeFileHandle.Dispose();
    }
}

public record DatabaseHeader
{
    public int Version { get; init; }
    public int PageDirectoryPageNumber { get; init; }
    public int TableDirectoryPageNumber { get; init; }
}