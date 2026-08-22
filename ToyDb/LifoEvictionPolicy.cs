using System.Collections.ObjectModel;

namespace ToyDb;

public interface IEvictionPolicy
{
    bool TryEvict(out int frameEvicted);
    void FreePage(int pageNumber);
    void UsePage(int pageNumber);
}

public class LifoEvictionPolicy(ReadOnlyDictionary<int, BufferTableEntry> pageBufferTable) : IEvictionPolicy
{
    private readonly ReadOnlyDictionary<int, BufferTableEntry> _pageBufferTable = pageBufferTable;
    private readonly PriorityQueue<int, long> _evictionQueue = new(
        Comparer<long>.Create(static (left, right) => right.CompareTo(left)));

    private long _nextPriority;

    public bool TryEvict(out int frameEvicted)
    {
        while (_evictionQueue.TryDequeue(out var pageNumber, out _))
        {
            if (!_pageBufferTable.TryGetValue(pageNumber, out var entry) || entry.PinCount != 0)
                continue;

            frameEvicted = entry.FrameNumber;
            return true;
        }

        frameEvicted = 0;
        return false;
    }

    public void FreePage(int pageNumber)
    {
        _evictionQueue.Remove(pageNumber, out _, out _);
        _evictionQueue.Enqueue(pageNumber, ++_nextPriority);
    }

    public void UsePage(int pageNumber) =>
        _evictionQueue.Remove(pageNumber, out _, out _);
}
