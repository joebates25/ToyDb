using System.Collections;
using Microsoft.Extensions.Logging;

namespace ToyDb;

using PageNumber = int;
using FrameNumber = int;

public class PageBufferManager : IDisposable
{
    private readonly ILogger _logger;

    private readonly FileIoManager _fileIoManager;

    private readonly Memory<byte> _bufferPool;

    private readonly Dictionary<PageNumber, FrameNumber> _pageBufferTable = new();
    private readonly Stack<int> _freeFrames;

    public PageBufferManager(FileIoManager fileIoManager, PageBufferConfig? pageBufferConfig)
    {
        var frameCount = pageBufferConfig?.FrameCount ?? 2_000;

        _fileIoManager = fileIoManager;
        _logger        = Logging.LoggerFactory.CreateLogger<FileIoManager>();
        _bufferPool    = new byte[Constants.PageSizeBytes * frameCount];
        _freeFrames    = new Stack<int>(Enumerable.Range(0, frameCount).Reverse());
    }

    public async Task<TPage> ReadPageAsync<TPage>(int pageNumber) where TPage : Page, IPageFactory<TPage>
    {
        _logger.Log(LogLevel.Information, $"Reading page {pageNumber}");
        var hasPage = _pageBufferTable.TryGetValue(pageNumber, out var frameNumber);

        if (hasPage) return TPage.CreatePage(GetBufferFrame(frameNumber));

        frameNumber = GetFirstFreeFrameNumber();
        var bufferSlice =
            GetBufferFrame(frameNumber);
        bufferSlice.Span.Clear();
        await _fileIoManager.ReadAsync(pageNumber * Constants.PageSizeBytes, bufferSlice);
        _pageBufferTable.Add(pageNumber, frameNumber);
        return TPage.CreatePage(bufferSlice);
    }

    public TPage AllocatePage<TPage>(int pageNumber) where TPage : Page, IPageFactory<TPage>
    {
        _logger.Log(LogLevel.Information, $"Allocating page {pageNumber}");
        if (HasPage(pageNumber))
            throw new InvalidOperationException($"Page {pageNumber} already allocated");

        var firstFreeFrameNumber = GetFirstFreeFrameNumber();
        var bufferSlice =
            _bufferPool.Slice(firstFreeFrameNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);
        bufferSlice.Span.Fill(0);
        _pageBufferTable.Add(pageNumber, firstFreeFrameNumber);

        return TPage.InitializePage(bufferSlice);
    }

    public async Task FlushAsync()
    {
        _logger.Log(LogLevel.Information, "Flushing page buffers");
        foreach (var pageBufferTableEntry in _pageBufferTable)
        {
            var frame = pageBufferTableEntry.Value;
            var pageMemory =
                (ReadOnlyMemory<byte>) GetBufferFrame(frame);
            await _fileIoManager.WriteAsync(pageBufferTableEntry.Key * Constants.PageSizeBytes, pageMemory);
        }

        await _fileIoManager.FlushAsync();
    }

    private FrameNumber GetFirstFreeFrameNumber() => _freeFrames.Pop();

    private bool HasPage(int pageNumber) => _pageBufferTable.ContainsKey(pageNumber);

    private Memory<byte> GetBufferFrame(int frameNumber) =>
        _bufferPool.Slice(frameNumber * Constants.PageSizeBytes, Constants.PageSizeBytes);

    public void Dispose()
    {
        _fileIoManager.FlushAsync().GetAwaiter().GetResult();
        _fileIoManager.Dispose();
    }
}

public record PageBufferConfig(int FrameCount);