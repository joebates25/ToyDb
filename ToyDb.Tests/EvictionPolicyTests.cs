using System.Collections.ObjectModel;

namespace ToyDb.Tests;

public class EvictionPolicyTests
{
    [Test]
    public void EvictsLeastRecentlyFreedPageFirst()
    {
        var policy = CreatePolicy(1, 2);

        policy.MarkPageNotInUse(1);
        Thread.Sleep(1);
        policy.MarkPageNotInUse(2);

        Assert.That(policy.TryEvict(out var frameNumber), Is.True);
        Assert.That(frameNumber, Is.EqualTo(1));
    }

    [Test]
    public void PageInUseIsNoLongerEligibleForEviction()
    {
        var policy = CreatePolicy(1, 2);

        policy.MarkPageNotInUse(1);
        policy.MarkPageNotInUse(2);
        policy.MarkPageInUse(2);

        Assert.That(policy.TryEvict(out var frameNumber), Is.True);
        Assert.That(frameNumber, Is.EqualTo(1));
        Assert.That(policy.TryEvict(out _), Is.False);
    }

    [Test]
    public void FreeingPageMoreThanOnceDoesNotAddDuplicateCandidates()
    {
        var policy = CreatePolicy(1);

        policy.MarkPageNotInUse(1);
        policy.MarkPageNotInUse(1);

        Assert.That(policy.TryEvict(out var frameNumber), Is.True);
        Assert.That(frameNumber, Is.EqualTo(1));
        Assert.That(policy.TryEvict(out _), Is.False);
    }

    private static LruEvictionPolicy CreatePolicy(params int[] pageNumbers)
    {
        var pageBufferTable = pageNumbers.ToDictionary(
            pageNumber => pageNumber,
            pageNumber => new BufferTableEntry(pageNumber, Dirty: false, PinCount: 0));

        return new LruEvictionPolicy(
            new ReadOnlyDictionary<int, BufferTableEntry>(pageBufferTable));
    }
}
