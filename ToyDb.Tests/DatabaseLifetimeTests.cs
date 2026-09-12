namespace ToyDb.Tests;

public class DatabaseLifetimeTests
{
    [Test]
    public async Task OpenDatabasesKeepTheirServicesIsolated()
    {
        var firstPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");
        var secondPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");
        try
        {
            using var first = await Database.OpenAsync(firstPath);
            using var second = await Database.OpenAsync(secondPath);
            await first.AddSchemaAsync(new Schema("Items").AddField("Id", SchemaFieldType.Integer, sizeof(int)));
            await second.AddSchemaAsync(new Schema("Items").AddField("Id", SchemaFieldType.Integer, sizeof(int)));
            await first.InsertAsync("Items", ["Id"], [[1]]);
            await first.CloseAsync();
            await first.CloseAsync();
            first.Dispose();

            await second.InsertAsync("Items", ["Id"], [[2]]);
            var secondRows = await second.ExecuteSqlQuery("SELECT Id FROM Items").ToListAsync();
            Assert.That(secondRows.Select(row => row[0]), Is.EqualTo(new[] { 2 }));

            using var reopened = await Database.OpenAsync(firstPath);
            var firstRows = await reopened.ExecuteSqlQuery("SELECT Id FROM Items").ToListAsync();
            Assert.That(firstRows.Select(row => row[0]), Is.EqualTo(new[] { 1 }));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [Test]
    public async Task FailedOpenReleasesTheDatabaseFile()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");
        try
        {
            await File.WriteAllBytesAsync(path, new byte[Constants.PageSizeBytes]);
            Assert.ThrowsAsync<Exception>(async () => await Database.OpenAsync(path));

            using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.That(file.CanWrite, Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
