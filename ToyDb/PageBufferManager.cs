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

    // Frames that currently have no page assigned. This initially contains the entire buffer pool
    // and receives a frame again if assigning a page to it fails.
    private readonly Stack<int> _unassignedFrames;
    private readonly IEvictionPolicy _evictionPolicy;
    private readonly HashSet<int> _dirtyPages = new();

    public PageBufferManager(FileIoManager fileIoManager, PageBufferConfig? pageBufferConfig,
        ILoggerFactory loggerFactory)
    {
        var frameCount = pageBufferConfig?.FrameCount ?? 2_000;

        _fileIoManager    = fileIoManager;
        _logger           = loggerFactory.CreateLogger<PageBufferManager>();
        _bufferPool       = new byte[Constants.PageSizeBytes * frameCount];
        _unassignedFrames = new Stack<int>(Enumerable.Range(0, frameCount).Reverse());
        _evictionPolicy = new LruEvictionPolicy(
            new ReadOnlyDictionary<int, BufferTableEntry>(_pageBufferTable));
    }

    public async Task<PageLease<TPage>> LeasePageAsync<TPage>(int pageNumber) where TPage : Page, IPageFactory<TPage>
    {
        _logger.Log(LogLevel.Information, $"Reading page {pageNumber}");
        if (_pageBufferTable.TryGetValue(pageNumber, out var frame))
        {
            _pageBufferTable[pageNumber] = frame with {PinCount = frame.PinCount + 1};
            _evictionPolicy.MarkPageInUse(pageNumber);
            return new PageLease<TPage>(TPage.CreatePage(GetBufferFrame(frame.FrameNumber)), OnFreePage, OnDirtyPage,
                pageNumber);
        }

        var frameNumber = FreeFrame();
        try
        {
            var bufferSlice = GetBufferFrame(frameNumber);
            bufferSlice.Span.Clear();
            _logger.Log(LogLevel.Information, "Leasing page {PageNumber}", pageNumber);
            await _fileIoManager.ReadAsync(pageNumber * Constants.PageSizeBytes, bufferSlice);

            var page = TPage.CreatePage(bufferSlice);
            _pageBufferTable.Add(pageNumber, BufferTableEntry.Create(frameNumber));
            _evictionPolicy.MarkPageInUse(pageNumber);

            return new PageLease<TPage>(page, OnFreePage, OnDirtyPage, pageNumber);
        }
        catch
        {
            ReturnUnassignedFrame(pageNumber, frameNumber);
            throw;
        }
    }

    public PageLease<TPage> AllocatePageLease<TPage>(int pageNumber) where TPage : Page, IPageFactory<TPage>
    {
        _logger.Log(LogLevel.Information, "Allocating page {PageNumber}", pageNumber);
        if (HasPage(pageNumber))
            throw new InvalidOperationException($"Page {pageNumber} already allocated");

        var firstFreeFrameNumber = FreeFrame();
        try
        {
            var bufferSlice = GetBufferFrame(firstFreeFrameNumber);
            bufferSlice.Span.Clear();
            var page = TPage.InitializePage(bufferSlice);
            _pageBufferTable.Add(pageNumber, BufferTableEntry.CreateDirty(firstFreeFrameNumber));
            _dirtyPages.Add(pageNumber);

            return new PageLease<TPage>(page, OnFreePage, OnDirtyPage, pageNumber);
        }
        catch
        {
            ReturnUnassignedFrame(pageNumber, firstFreeFrameNumber);
            throw;
        }
    }

    private void OnFreePage(int pageNumber)
    {
        // Page is not even allocated -- abort
        if (!_pageBufferTable.TryGetValue(pageNumber, out var frame)) return;

        var newPinCount = frame.PinCount > 0 ? frame.PinCount - 1 : 0;
        _pageBufferTable[pageNumber] = frame with {PinCount = newPinCount};

        if (newPinCount == 0) _evictionPolicy.MarkPageNotInUse(pageNumber);
    }

    private void OnDirtyPage(int pageNumber)    
    {
        if (!_pageBufferTable.TryGetValue(pageNumber, out var frame)) return;

        _pageBufferTable[pageNumber] = frame with {Dirty = true};
        _dirtyPages.Add(pageNumber);
    }

    public async Task FlushAsync()
    {
        _logger.Log(LogLevel.Information, "Flushing page buffers");
        foreach (var dirtyPage in _dirtyPages.ToArray())
        {
            var dirtyFrame = _pageBufferTable[dirtyPage];
            if (dirtyFrame.InUse) continue;

            var pageMemory =
                (ReadOnlyMemory<byte>) GetBufferFrame(dirtyFrame.FrameNumber);
            await _fileIoManager.WriteAsync(dirtyPage * Constants.PageSizeBytes, pageMemory);
            _pageBufferTable[dirtyPage] = dirtyFrame with {Dirty = false};
            _dirtyPages.Remove(dirtyPage);
            _evictionPolicy.MarkPageNotInUse(dirtyPage);
            _logger.Log(LogLevel.Information, "Flushed dirty page {PageNumber} to disk", dirtyPage);
        }

        await _fileIoManager.FlushAsync();
    }

    public void Dispose()
    {
        _fileIoManager.FlushAsync().GetAwaiter().GetResult();
        _fileIoManager.Dispose();
    }

    private FrameNumber FreeFrame()
    {
        // todo: perhaps eventually there's a timeout/retry mechanism to wait for a frame to become available,
        // but for now we will just throw an exception.
        if (_unassignedFrames.Count > 0)
            return _unassignedFrames.Pop();

        if (TryEvictPage(out var freeFrameNumber))
            return freeFrameNumber;

        if (_dirtyPages.Count > 0)
        {
            _logger.Log(LogLevel.Information, "Flushing dirty pages to free up buffer space");
            FlushAsync().GetAwaiter().GetResult();
            if (TryEvictPage(out freeFrameNumber))
                return freeFrameNumber;
        }

        throw new OutOfMemoryException(
            "All buffer frames are in use and no pages can be evicted. Consider increasing the buffer size.");
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

    private void ReturnUnassignedFrame(int pageNumber, int frameNumber)
    {
        if (_pageBufferTable.TryGetValue(pageNumber, out var entry)
            && entry.FrameNumber == frameNumber)
        {
            _pageBufferTable.Remove(pageNumber);
            _dirtyPages.Remove(pageNumber);
            _evictionPolicy.MarkPageInUse(pageNumber);
        }

        _unassignedFrames.Push(frameNumber);
    }

    private bool HasPage(int pageNumber) => _pageBufferTable.ContainsKey(pageNumber);

    private Memory<byte> GetBufferFrame(int frameNumber) =>
        _bufferPool.Slice(frameNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);

    public void MarkPageDirty(int pageNumber)
    {
        if (_pageBufferTable.TryGetValue(pageNumber, out var frame))
        {
            _pageBufferTable[pageNumber] = frame with {Dirty = true};
            _dirtyPages.Add(pageNumber);
        }
    }
}

public record BufferTableEntry(int FrameNumber, bool Dirty, int PinCount)
{
    public static BufferTableEntry Create(int frameNumber) => new(frameNumber, false, 1);

    public static BufferTableEntry CreateDirty(int frameNumber) => new(frameNumber, true, 1);

    public bool InUse => PinCount > 0;
}

public record PageBufferConfig(int FrameCount);
