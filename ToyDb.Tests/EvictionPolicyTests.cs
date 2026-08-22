using System.Collections.ObjectModel;

namespace ToyDb.Tests;

public class EvictionPolicyTests
{
    [Test]
    public void EvictsMostRecentlyFreedPageFirst()
    {
        var policy = CreatePolicy(1, 2);

        policy.FreePage(1);
        policy.FreePage(2);

        Assert.That(policy.TryEvict(out var frameNumber), Is.True);
        Assert.That(frameNumber, Is.EqualTo(2));
    }

    [Test]
    public void PageInUseIsNoLongerEligibleForEviction()
    {
        var policy = CreatePolicy(1, 2);

        policy.FreePage(1);
        policy.FreePage(2);
        policy.UsePage(2);

        Assert.That(policy.TryEvict(out var frameNumber), Is.True);
        Assert.That(frameNumber, Is.EqualTo(1));
        Assert.That(policy.TryEvict(out _), Is.False);
    }

    [Test]
    public void FreeingPageMoreThanOnceDoesNotAddDuplicateCandidates()
    {
        var policy = CreatePolicy(1);

        policy.FreePage(1);
        policy.FreePage(1);

        Assert.That(policy.TryEvict(out var frameNumber), Is.True);
        Assert.That(frameNumber, Is.EqualTo(1));
        Assert.That(policy.TryEvict(out _), Is.False);
    }

    private static LifoEvictionPolicy CreatePolicy(params int[] pageNumbers)
    {
        var pageBufferTable = pageNumbers.ToDictionary(
            pageNumber => pageNumber,
            pageNumber => new BufferTableEntry(pageNumber, Dirty: false, PinCount: 0));

        return new LifoEvictionPolicy(
            new ReadOnlyDictionary<int, BufferTableEntry>(pageBufferTable));
    }
}
