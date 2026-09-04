using System.Collections.ObjectModel;

namespace ToyDb;

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
            if (!pageBufferTable.TryGetValue(pageNumber, out var entry) || entry.PinCount != 0 ||
                entry.Dirty) // todo: but need to write out dirty pages at some point
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