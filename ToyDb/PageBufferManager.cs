using Microsoft.Extensions.Logging;

namespace ToyDb;

using System.Collections.ObjectModel;
using PageNumber = int;
using FrameNumber = int;

public class PageBufferManager : IDisposable
{
    private readonly ILogger _logger;

    private readonly FileIoManager _fileIoManager;

    private readonly Memory<byte> _bufferPool;

    private readonly Dictionary<PageNumber, BufferTableEntry> _pageBufferTable = new();
    private readonly Stack<int> _freeFrames;
    private readonly IEvictionPolicy _evictionPolicy;

    public PageBufferManager(FileIoManager fileIoManager, PageBufferConfig? pageBufferConfig)
    {
        var frameCount = pageBufferConfig?.FrameCount ?? 2_000;

        _fileIoManager = fileIoManager;
        _logger        = Logging.LoggerFactory.CreateLogger<FileIoManager>();
        _bufferPool    = new byte[Constants.PageSizeBytes * frameCount];
        _freeFrames    = new Stack<int>(Enumerable.Range(0, frameCount).Reverse());
        _evictionPolicy = new LruEvictionPolicy(
            new ReadOnlyDictionary<int, BufferTableEntry>(_pageBufferTable));
    }

    public async Task<TPage> ReadPageAsync<TPage>(int pageNumber) where TPage : Page, IPageFactory<TPage>
    {
        _logger.Log(LogLevel.Information, $"Reading page {pageNumber}");
        if (_pageBufferTable.TryGetValue(pageNumber, out var frame))
        {
            _pageBufferTable[pageNumber] = frame with {PinCount = frame.PinCount + 1};
            _evictionPolicy.MarkPageInUse(pageNumber);
            return TPage.CreatePage(GetBufferFrame(frame.FrameNumber));
        }

        // todo: if anything fails, frame stays unfree. need to fix
        var frameNumber = GetFirstFreeFrameNumber();
        var bufferSlice = GetBufferFrame(frameNumber);
        bufferSlice.Span.Clear();
        await _fileIoManager.ReadAsync(pageNumber * Constants.PageSizeBytes, bufferSlice);

        _pageBufferTable.Add(pageNumber, BufferTableEntry.Create(frameNumber));
        _evictionPolicy.MarkPageInUse(pageNumber);

        return TPage.CreatePage(bufferSlice);
    }

    public TPage AllocatePage<TPage>(int pageNumber) where TPage : Page, IPageFactory<TPage>
    {
        _logger.Log(LogLevel.Information, "Allocating page {PageNumber}", pageNumber);
        if (HasPage(pageNumber))
            throw new InvalidOperationException($"Page {pageNumber} already allocated");

        var firstFreeFrameNumber = GetFirstFreeFrameNumber();
        var bufferSlice =
            _bufferPool.Slice(firstFreeFrameNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
        bufferSlice.Span.Fill(0);
        _pageBufferTable.Add(pageNumber, BufferTableEntry.CreateDirty(firstFreeFrameNumber));

        return TPage.InitializePage(bufferSlice);
    }

    public void DirtyPage(int pageNumber)
    {
        // Do NOT call MarkPageNotInUse because dirty pages shouldn't be evicted. Write them to disk instead.
    }

    // todo: page probably needs page number at this point
    public void FreePage(int pageNumber)
    {
        // Page is not allocated -- abort
        if (!_pageBufferTable.TryGetValue(pageNumber, out var frame)) return;

        var newPinCount = frame.PinCount > 0 ? frame.PinCount - 1 : 0;
        _pageBufferTable[pageNumber] = frame with {PinCount = newPinCount};

        if (frame.PinCount == 1) // Page no longer has any more pins, can be freed up
        {
            _evictionPolicy.MarkPageNotInUse(pageNumber);
        }
    }

    public async Task FlushAsync()
    {
        _logger.Log(LogLevel.Information, "Flushing page buffers");
        foreach (var pageBufferTableEntry in _pageBufferTable)
        {
            var frame = pageBufferTableEntry.Value;
            var pageMemory =
                (ReadOnlyMemory<byte>) GetBufferFrame(frame.FrameNumber);
            await _fileIoManager.WriteAsync(pageBufferTableEntry.Key * Constants.PageSizeBytes, pageMemory);
        }

        await _fileIoManager.FlushAsync();
    }

    public void Dispose()
    {
        _fileIoManager.FlushAsync().GetAwaiter().GetResult();
        _fileIoManager.Dispose();
    }

    private FrameNumber GetFirstFreeFrameNumber()
    {
        if (_freeFrames.Count > 0)
            return _freeFrames.Pop();

        if (TryEvictPage(out var freeFrameNumber))
            return freeFrameNumber;

        throw new NotImplementedException(
            "todo: Need to handle case when all frames are in use and no more buffer space available");
    }

    private bool TryEvictPage(out int evictFrame)
    {
        if (!_evictionPolicy.TryEvict(out evictFrame)) return false;

        var frameNumber = evictFrame;
        var pageNumber = _pageBufferTable
            .First(entry => entry.Value.FrameNumber == frameNumber)
            .Key;
        _pageBufferTable.Remove(pageNumber);
        return true;
    }

    private bool HasPage(int pageNumber) => _pageBufferTable.ContainsKey(pageNumber);

    private Memory<byte> GetBufferFrame(int frameNumber) =>
        _bufferPool.Slice(frameNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
}

public record BufferTableEntry(int FrameNumber, bool Dirty, int PinCount)
{
    public static BufferTableEntry Create(int frameNumber) => new(frameNumber, false, 1);

    public static BufferTableEntry CreateDirty(int frameNumber) => new(frameNumber, true, 1);
}

public record PageBufferConfig(int FrameCount);