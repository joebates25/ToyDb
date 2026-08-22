using ToyDb.Pages;

namespace ToyDb.Tests;

public class PageBufferManagerTests
{
    [Test]
    public void EvictedFrameIsNotAlsoAddedToFreeFrames()
    {
        var databasePath = GetTempDatabasePath();

        try
        {
            using var manager = CreateManager(databasePath);
            manager.AllocatePage<DataPage>(0);
            manager.FreePage(0);

            manager.AllocatePage<DataPage>(1);

            Assert.That(
                () => manager.AllocatePage<DataPage>(2),
                Throws.TypeOf<NotImplementedException>());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Test]
    public async Task ReadingCachedPagePinsItAndRemovesItFromEvictionPolicy()
    {
        var databasePath = GetTempDatabasePath();

        try
        {
            using var manager = CreateManager(databasePath);
            await manager.ReadPageAsync<DataPage>(0);
            manager.FreePage(0);

            await manager.ReadPageAsync<DataPage>(0);

            Assert.That(
                () => manager.AllocatePage<DataPage>(1),
                Throws.TypeOf<NotImplementedException>());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static PageBufferManager CreateManager(string databasePath) =>
        new(new FileIoManager(databasePath), new PageBufferConfig(FrameCount: 1));

    private static string GetTempDatabasePath() =>
        Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");
}
