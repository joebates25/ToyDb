namespace ToyDb;

public class PageLease<T>(T page, PageLease<T>.ReleasePageDelegate releasePageDelegate, int pageNumber)
    : IDisposable
    where T : Page
{
    private bool _disposed;
    public T Page {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return page;
        }
    }

    private int PageNumber { get; } = pageNumber;

    public void Dispose()
    {
        _disposed = true;
        releasePageDelegate?.Invoke(PageNumber);
    }

    public delegate void ReleasePageDelegate(int pageNumber);
}
