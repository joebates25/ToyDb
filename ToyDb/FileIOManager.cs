using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace ToyDb;

public partial class FileIoManager(string fileName) : IDisposable
{
    private readonly SafeFileHandle _safeFileHandle = File.OpenHandle(
        fileName,
        FileMode.OpenOrCreate,
        FileAccess.ReadWrite,
        FileShare.ReadWrite);

    private ILogger Logger { get; } = Logging.LoggerFactory.CreateLogger<FileIoManager>();

    public async Task WriteAsync(int offset, ReadOnlyMemory<byte> source)
    {
        LogWritingDataLengthLengthToOffsetOffset(Logger, source.Length, offset);
        await RandomAccess.WriteAsync(_safeFileHandle, source, offset);
    }

    public async Task ReadAsync(int offset, Memory<byte> destination)
    {
        LogReadingDataLengthLengthFromOffsetOffset(Logger, destination.Length, offset);
        await RandomAccess.ReadAsync(_safeFileHandle, destination, offset);
    }

    public Task FlushAsync()
    {
        RandomAccess.FlushToDisk(_safeFileHandle);
        return Task.CompletedTask;
    }

    public void Dispose() => _safeFileHandle.Dispose();

    [LoggerMessage(LogLevel.Information, "Writing data length {length} to offset {offset}...")]
    static partial void LogWritingDataLengthLengthToOffsetOffset(ILogger logger, int length, int offset);

    [LoggerMessage(LogLevel.Information, "Reading data length {length} from offset {offset}...")]
    static partial void LogReadingDataLengthLengthFromOffsetOffset(ILogger logger, int length, int offset);
}