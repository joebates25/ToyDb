using System.Collections;

namespace ToyDb;

public class PageBufferManager(FileIoManager fileIoManager, PageBufferConfig? pageBufferConfig) : IDisposable
{
    private readonly Memory<byte> _shittyBigAssBuffer =
        new byte[Constants.PageSizeBytes * pageBufferConfig?.FrameCount ?? 2_000];

    private readonly HashSet<int> _pageBufferTable = new();

    public async Task<TPage> ReadPageAsync<TPage>(int pageNumber) where TPage : Page, IPageFactory<TPage>
    {
        var bufferSlice = _shittyBigAssBuffer.Slice(pageNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
        if (!_pageBufferTable.Contains(pageNumber))
        {
            bufferSlice.Span.Fill(0);
            await fileIoManager.ReadAsync(pageNumber * Constants.PageSizeBytes, bufferSlice);
            _pageBufferTable.Add(pageNumber);
        }

        return TPage.CreatePage(bufferSlice);
    }

    public TPage AllocatePage<TPage>(int pageNumber) where TPage : Page, IPageFactory<TPage>
    {
        if (_pageBufferTable.Contains(pageNumber)) throw new InvalidOperationException($"Page {pageNumber} already allocated");

        var bufferSlice = _shittyBigAssBuffer.Slice(pageNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
        bufferSlice.Span.Fill(0);
        _pageBufferTable.Add(pageNumber);

        return TPage.InitializePage(bufferSlice);
    }

    public async Task FlushAsync()
    {
        foreach (var pageNumber in _pageBufferTable)
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