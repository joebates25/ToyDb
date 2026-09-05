using ToyDb.Pages;

namespace ToyDb;

public abstract class Page(Memory<byte> data) : IDisposable
{
    private bool _isDisposed;

    public Memory<byte> Data => _isDisposed ? throw new ObjectDisposedException(nameof(Page)) : field;

    public void Dispose()
    {
        if (!_isDisposed) _isDisposed = true;
    }
}

public interface IPageFactory<out TPage> where TPage : Page
{
    static abstract TPage CreatePage(Memory<byte> data);

    static abstract TPage InitializePage(Memory<byte> data);
}