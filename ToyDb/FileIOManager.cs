using Microsoft.Win32.SafeHandles;

namespace ToyDb;

public class FileIoManager(string fileName) : IDisposable
{
    private readonly SafeFileHandle _safeFileHandle = File.OpenHandle(
        fileName,
        FileMode.OpenOrCreate,
        FileAccess.ReadWrite,
        FileShare.ReadWrite);

    public async Task WriteAsync(int offset, ReadOnlyMemory<byte> source) =>
        await RandomAccess.WriteAsync(_safeFileHandle, source, offset);

    public async Task ReadAsync(int offset, Memory<byte> destination) =>
        await RandomAccess.ReadAsync(_safeFileHandle, destination, offset);

    public Task FlushAsync()
    {
        RandomAccess.FlushToDisk(_safeFileHandle);
        return Task.CompletedTask;
    }

    public void Dispose() => _safeFileHandle.Dispose();
}