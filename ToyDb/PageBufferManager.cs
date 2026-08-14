namespace ToyDb;

public class PageBufferManager(FileIoManager fileIoManager, PageBufferConfig? pageBufferConfig) : IDisposable
{
    private readonly Memory<byte> _shittyBigAssBuffer =
        new byte[Constants.PageSizeBytes * pageBufferConfig?.FrameCount ?? 2_000];

    private readonly Dictionary<int, bool> _pageBufferTable = new();

    public async Task<Page> ReadPageAsync(int pageNumber)
    {
        var bufferSlice = _shittyBigAssBuffer.Slice(pageNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
        if (!_pageBufferTable.ContainsKey(pageNumber) || !_pageBufferTable[pageNumber])
        {
            await fileIoManager.ReadAsync(pageNumber * Constants.PageSizeBytes, bufferSlice);
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
            await fileIoManager.WriteAsync(pageNumber * Constants.PageSizeBytes, pageMemory);
        }

        await fileIoManager.FlushAsync();
    }

    public void Dispose()
    {
        fileIoManager.Dispose();
    }
}

public record PageBufferConfig(int FrameCount);