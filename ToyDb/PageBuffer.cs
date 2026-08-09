using Microsoft.Win32.SafeHandles;

namespace ToyDb;

public class PageBuffer(SafeFileHandle safeHandle)
{
    private readonly Memory<byte> _headerPageMemory = new byte[Constants.PageSizeBytes];

    public async Task<Page> GetPageAsync(int pageNumber)
    {
        await RandomAccess.ReadAsync(safeHandle, _headerPageMemory, pageNumber * Constants.PageSizeBytes);
        return new Page(_headerPageMemory);
    }
}