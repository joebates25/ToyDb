using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ToyDb.Pages;

/// <summary>
/// Contains the basic information about the database for functioning
/// </summary>
public class DatabaseHeaderPage(Memory<byte> data) : Page(data), IPageFactory<DatabaseHeaderPage>
{
    /*
        Database header page layout (4096 bytes):
        +-------------------------------------------+ byte 0
        | WelcomeMessage (17 bytes, UTF-8)          |
        +-------------------------------------------+ byte 17
        | Version (4 bytes, LE)                     |
        +-------------------------------------------+ byte 21
        | PageCount (4 bytes, LE)                   |
        +-------------------------------------------+ byte 25
        | SchemaDirectoryPageNumber (4 bytes, LE)   |
        +-------------------------------------------+ byte 29
        | Unused                                    |
        +-------------------------------------------+ byte 4096
    */
    private const int WelcomeMessageLengthBytes = 17;
    private const int DatabaseHeaderSize = WelcomeMessageLengthBytes + 3 * sizeof(int);

    [InlineArray(WelcomeMessageLengthBytes)]
    private struct WelcomeMessageBuffer
    {
        private byte _element;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DatabaseHeader
    {
        internal WelcomeMessageBuffer WelcomeMessage;
        internal int Version;
        internal int PageCount;
        internal int SchemaDirectoryPageNumber;
    }

    static DatabaseHeaderPage()
    {
        if (DatabaseHeaderSize != Unsafe.SizeOf<DatabaseHeader>())
            throw new InvalidOperationException("Database header size is invalid");

        if (Encoding.UTF8.GetByteCount(Constants.WelcomeMessage) != WelcomeMessageLengthBytes)
            throw new InvalidOperationException("Database welcome message size is invalid");
    }

    private ref DatabaseHeader Header => ref MemoryMarshal.AsRef<DatabaseHeader>(Data.Span);

    public string WelcomeMessage => Encoding.UTF8.GetString(Header.WelcomeMessage);

    public int Version
    {
        get => Header.Version;
        set => Header.Version = value;
    }
    
    public int PageCount
    {
        get => Header.PageCount;
        set => Header.PageCount = value;
    }

    public int SchemaDirectoryPageNumber
    {
        get => Header.SchemaDirectoryPageNumber;
        set => Header.SchemaDirectoryPageNumber = value;
    }

    public static DatabaseHeaderPage CreatePage(Memory<byte> data)
    {
        return new DatabaseHeaderPage(data);
    }

    public static DatabaseHeaderPage InitializePage(Memory<byte> data)
    {
        var page = new DatabaseHeaderPage(data);
        page.Header.WelcomeMessage = default;
        Encoding.UTF8.GetBytes(Constants.WelcomeMessage).CopyTo(page.Header.WelcomeMessage);
        return page;
    }
}
