using System.Buffers.Binary;
using System.Text;

namespace ToyDb;

public class DatabaseHeaderPage : Page
{
    private const int WelcomeMessageLength = 17;

    private const int VersionOffset = 17;
    private const int PageDirectoryOffset = 21;
    private const int SchemaDirectoryOffset = 25;

    public DatabaseHeaderPage(Memory<byte> data) : base(data)
    {
        Encoding.UTF8.GetBytes(Constants.WelcomeMessage).CopyTo(data.Span);
    }

    public string WelcomeMessage => Encoding.UTF8.GetString(Data.Span[..WelcomeMessageLength]);

    public int Version => BitConverter.ToInt32(Data.Span[VersionOffset..]);

    public void SetVersion(int version) =>
        BinaryPrimitives.WriteInt32LittleEndian(Data.Span[VersionOffset..], version);

    public int PageDirectoryPageNumber => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[PageDirectoryOffset..]);

    public void SetPageDirectoryPageNumber(int pageDirectoryPageNumber) =>
        BinaryPrimitives.WriteInt32LittleEndian(Data.Span[PageDirectoryOffset..], pageDirectoryPageNumber);

    public int SchemaDirectoryPageNumber => BitConverter.ToInt32(Data.Span[SchemaDirectoryOffset..]);

    public void SetSchemaDirectoryPageNumber(int schemaDirectoryPageNumber) =>
        BinaryPrimitives.WriteInt32LittleEndian(Data.Span[SchemaDirectoryOffset..], schemaDirectoryPageNumber);
}