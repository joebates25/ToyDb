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

    public int Version
    {
        get => BitConverter.ToInt32(Data.Span[VersionOffset..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[VersionOffset..], value);
    }
    
    public int PageCount
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[PageCountOffset..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[PageCountOffset..], value);
    }

    public int SchemaDirectoryPageNumber
    {
        get => BitConverter.ToInt32(Data.Span[SchemaDirectoryOffset..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[SchemaDirectoryOffset..], value);
    }

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