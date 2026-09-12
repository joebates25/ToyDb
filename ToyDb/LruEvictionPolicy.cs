using System.Collections.ObjectModel;

namespace ToyDb;

/// <summary>
/// Determines WHICH pages to evict from the buffer pool when a new page
/// needs to be loaded and there are no free frames available.
///
/// A page is eligible for eviction if it is not in use (pin count is zero)
/// and is not dirty (has no unflushed changes).
///
/// The database engine will tell the eviction policy when a page is no longer in use (pin count drops to zero)
/// and when a page is in use (pin count increases from zero). The eviction policy will then use this information
/// to determine which pages are eligible for eviction and in what order.
/// </summary>
public interface IEvictionPolicy
{
    bool TryEvict(out int frameEvicted);
    void MarkPageNotInUse(int pageNumber);
    void MarkPageInUse(int pageNumber);
}

public class LruEvictionPolicy(ReadOnlyDictionary<int, BufferTableEntry> pageBufferTable) : IEvictionPolicy
{
    private readonly PriorityQueue<int, long> _evictionQueue = new();

    public bool TryEvict(out int frameEvicted)
    {
        while (_evictionQueue.TryDequeue(out var pageNumber, out _))
        {
            if (!pageBufferTable.TryGetValue(pageNumber, out var entry)
                || entry.InUse
                || entry.Dirty)
                continue;

            frameEvicted = entry.FrameNumber;
            return true;
        }

        frameEvicted = 0;
        return false;
    }

    public void MarkPageNotInUse(int pageNumber)
    {
        _evictionQueue.Remove(pageNumber, out _, out _);
        _evictionQueue.Enqueue(pageNumber, DateTime.Now.Ticks);
    }

    public void MarkPageInUse(int pageNumber) =>
        _evictionQueue.Remove(pageNumber, out _, out _);
}