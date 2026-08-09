using System.Buffers.Binary;
using System.Text;

namespace ToyDb;

public class Page(Memory<byte> data)
{
    public int PageSize => Constants.PageSizeBytes;
    protected int PageType = 0;
    protected Memory<byte> Data = data;

    // problem: will need a new method here for every page
    public HeaderPage AsHeaderPage()
    {
        return new HeaderPage(Data);
    }
}

public class HeaderPage : Page
{
    private const int WelcomeMessageLength = 17;

    private const int VersionOffset = 17;
    private const int PageDirectoryOffset = 21;
    private const int TableDirectoryOffset = 25;

    public HeaderPage(Memory<byte> data): base(data)
    {
        Encoding.UTF8.GetBytes(Constants.WelcomeMessage).CopyTo(data.Span);
    }

    public string WelcomeMessage => Encoding.UTF8.GetString(Data.Span[..WelcomeMessageLength]);

    public int Version => BitConverter.ToInt32(Data.Span[VersionOffset..]);

    public int PageDirectoryPageNumber => BitConverter.ToInt32(Data.Span[PageDirectoryOffset..]);

    public int TableDirectoryPageNumber => BitConverter.ToInt32(Data.Span[TableDirectoryOffset..]);
}