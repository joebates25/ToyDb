namespace ToyDb;

public class PageBufferManager(FileIoManager fileIoManager, PageBufferConfig? pageBufferConfig) : IDisposable
{
    private readonly Memory<byte> _shittyBigAssBuffer =
        new byte[Constants.PageSizeBytes * pageBufferConfig?.FrameCount ?? 2_000];

    private readonly Dictionary<int, bool> _pageBufferTable = new();

    public async Task<TPage> ReadPageAsync<TPage>(int pageNumber) where TPage : Page, IPageFactory<TPage>
    {
        var bufferSlice = _shittyBigAssBuffer.Slice(pageNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
        if (!_pageBufferTable.ContainsKey(pageNumber) || !_pageBufferTable[pageNumber])
        {
            // todo: Consider clearing buffer slot
            await fileIoManager.ReadAsync(pageNumber * Constants.PageSizeBytes, bufferSlice);
            _pageBufferTable[pageNumber] = true;
        }

        return TPage.CreatePage(bufferSlice);
    }

    public TPage AllocatePage<TPage>(int pageNumber) where TPage : Page, IPageFactory<TPage>
    {
        if (_pageBufferTable.ContainsKey(pageNumber)) throw new InvalidOperationException($"Page {pageNumber} already allocated");

        // todo: Consider clearing buffer slot
        var bufferSlice = _shittyBigAssBuffer.Slice(pageNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
        _pageBufferTable[pageNumber] = true;

        return TPage.InitializePage(bufferSlice);
    }

    public async Task FlushAsync()
    {
        foreach (var pageNumber in _pageBufferTable.Keys)
        {
            var pageMemory =
                (ReadOnlyMemory<byte>) _shittyBigAssBuffer.Slice(pageNumber * Constants.PageSizeBytes,
                    Constants.PageSizeBytes);
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