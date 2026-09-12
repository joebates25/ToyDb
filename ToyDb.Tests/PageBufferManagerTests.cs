using ToyDb.Pages;

using Microsoft.Extensions.Logging.Abstractions;

namespace ToyDb.Tests;

public class PageBufferManagerTests
{
    [Test]
    public async Task EvictedFrameIsNotAlsoAddedToFreeFrames()
    {
        var databasePath = GetTempDatabasePath();

        try
        {
            await CreateDataPageAsync(databasePath);
            using var manager = CreateManager(databasePath);
            using (await manager.LeasePageAsync<DataPage>(0))
            {
            }

            using var allocatedPage = manager.AllocatePageLease<DataPage>(1);

            Assert.That(
                () => manager.AllocatePageLease<DataPage>(2),
                Throws.TypeOf<OutOfMemoryException>());
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
            await CreateDataPageAsync(databasePath);
            using var manager = CreateManager(databasePath);
            using (await manager.LeasePageAsync<DataPage>(0))
            {
            }

            using (await manager.LeasePageAsync<DataPage>(0))
            {
                Assert.That(
                    () => manager.AllocatePageLease<DataPage>(1),
                    Throws.TypeOf<OutOfMemoryException>());
            }

            using var allocatedPage = manager.AllocatePageLease<DataPage>(1);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static async Task CreateDataPageAsync(string databasePath)
    {
        var data = new byte[Constants.PageSizeBytes];
        using var page = DataPage.InitializePage(data);
        await File.WriteAllBytesAsync(databasePath, data);
    }

    private static PageBufferManager CreateManager(string databasePath) =>
        new(new FileIoManager(databasePath, NullLoggerFactory.Instance), new PageBufferConfig(FrameCount: 1),
            NullLoggerFactory.Instance);

    private static string GetTempDatabasePath() =>
        Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");
}
