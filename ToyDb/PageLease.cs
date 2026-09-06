namespace ToyDb;

public class PageLease<T>(
    T page,
    PageLease<T>.ReleasePageDelegate releasePageDelegate,
    PageLease<T>.DirtyPageDelegate dirtyPageDelegate,
    int pageNumber)
    : IDisposable
    where T : Page
{
    private bool _disposed;

    public T Page
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return page;
        }
    }

    public int PageNumber { get; } = pageNumber;

    public void Dispose()
    {
        _disposed = true;
        releasePageDelegate?.Invoke(PageNumber);
    }

    public delegate void ReleasePageDelegate(int pageNumber);

    public delegate void DirtyPageDelegate(int pageNumber);

    public void MarkDirty()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PageLease<T>));
        
        dirtyPageDelegate?.Invoke(PageNumber);
    }
}