using System.Buffers.Binary;
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
        | PageCount (4 bytes, LE)     |
        +-------------------------------------------+ byte 25
        | SchemaDirectoryPageNumber (4 bytes, LE)   |
        +-------------------------------------------+ byte 29
        | Unused                                    |
        +-------------------------------------------+ byte 4096
    */
    private const int WelcomeMessageLength = 17;

    private const int VersionOffset = 17;
    private const int PageCountOffset = 21;
    private const int SchemaDirectoryOffset = 25;
    
    public string WelcomeMessage => Encoding.UTF8.GetString(Data.Span[..WelcomeMessageLength]);

    public int Version => BitConverter.ToInt32(Data.Span[VersionOffset..]);

    public void SetVersion(int version) =>
        BinaryPrimitives.WriteInt32LittleEndian(Data.Span[VersionOffset..], version);

    public int PageCount => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[PageCountOffset..]);

    public void SetPageCount(int pageCount) =>
        BinaryPrimitives.WriteInt32LittleEndian(Data.Span[PageCountOffset..], pageCount);

    public int SchemaDirectoryPageNumber => BitConverter.ToInt32(Data.Span[SchemaDirectoryOffset..]);

    public void SetSchemaDirectoryPageNumber(int schemaDirectoryPageNumber) =>
        BinaryPrimitives.WriteInt32LittleEndian(Data.Span[SchemaDirectoryOffset..], schemaDirectoryPageNumber);

    public static DatabaseHeaderPage CreatePage(Memory<byte> data)
    {
        return new DatabaseHeaderPage(data);
    }

    public static DatabaseHeaderPage InitializePage(Memory<byte> data)
    {
        Encoding.UTF8.GetBytes(Constants.WelcomeMessage).CopyTo(data.Span);
        return new DatabaseHeaderPage(data);
    }
}