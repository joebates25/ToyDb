using Microsoft.Win32.SafeHandles;

namespace ToyDb;

public class PageBuffer(SafeFileHandle safeHandle)
{
    private readonly Memory<byte> _shittyBigAssBuffer = new byte[Constants.PageSizeBytes * 2_000];
    private readonly Dictionary<int, bool> _pageBufferTable = new();

    public async Task<Page> ReadPageAsync(int pageNumber)
    {
        var bufferSlice = _shittyBigAssBuffer.Slice(pageNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
        if (!_pageBufferTable.ContainsKey(pageNumber) || !_pageBufferTable[pageNumber])
        {
            await RandomAccess.ReadAsync(safeHandle, bufferSlice, pageNumber * Constants.PageSizeBytes);
            _pageBufferTable[pageNumber] = true;
        }

        return new Page(bufferSlice);
    }

    public Page AllocatePage(int pageNumber)
    {
        if (_pageBufferTable.ContainsKey(pageNumber)) throw new InvalidOperationException("Page already allocated");
        
        var bufferSlice = _shittyBigAssBuffer.Slice(pageNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
        _pageBufferTable[pageNumber] = true;

        return new Page(bufferSlice);
    }

    public async Task FlushAsync()
    {
        foreach (var pageNumber in _pageBufferTable.Keys)
        {
            ReadOnlyMemory<byte> pageMemory =
                _shittyBigAssBuffer.Slice(pageNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
            await RandomAccess.WriteAsync(safeHandle, pageMemory, pageNumber * Constants.PageSizeBytes);
        }
        RandomAccess.FlushToDisk(safeHandle);
    }
}