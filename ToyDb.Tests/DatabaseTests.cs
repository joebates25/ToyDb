namespace ToyDb.Tests;

public class DatabaseTests
{
    [Test]
    public async Task CanInitializeAndOpenDatabase()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");

        try
        {
            await Database.InitializeAsync(databasePath);

            using var database = Database.Open(databasePath);

            Assert.That(database.Info.Version, Is.GreaterThan(0));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Test]
    public async Task CanCreateBasicSchemaWithoutFields()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");

        try
        {
            await Database.InitializeAsync(databasePath);

            using var database = Database.Open(databasePath);
            await database.AddSchemaAsync(new Schema("TestSchema"));
            Assert.That(database.Info.Version, Is.GreaterThan(0));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Test]
    public async Task CanInsertData()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");

        try
        {
            await Database.InitializeAsync(databasePath);

            using (var database = Database.Open(databasePath))
            {
                await database.AddSchemaAsync(new Schema("TestSchema")
                    .AddField("field1", SchemaFieldType.Integer, 4));
            }

            using (var database = Database.Open(databasePath))
            {
                var inserted = await database.InsertAsync("TestSchema", ["field1"], [[4]]);

                Assert.That(inserted, Is.EqualTo(1));
            }
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Test]
    public async Task CanInsertAndSelectData()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");

        try
        {
            await Database.InitializeAsync(databasePath);

            using (var database = Database.Open(databasePath))
            {
                await database.AddSchemaAsync(new Schema("TestSchema")
                    .AddField("field1", SchemaFieldType.Integer, 4));
                await database.InsertAsync("TestSchema", ["field1"], [[4]]);
            }

            using (var database = Database.Open(databasePath))
            {
                var results = await database.SelectAsync("TestSchema", ["field1"]).ToListAsync();
                Assert.That(results.First().First(), Is.EqualTo(4));
            }
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Test]
    public async Task CanInsertAndDeleteData()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");

        try
        {
            await Database.InitializeAsync(databasePath);

            using (var database = Database.Open(databasePath))
            {
                await database.AddSchemaAsync(new Schema("TestSchema")
                    .AddField("field1", SchemaFieldType.Integer, 4));
                await database.InsertAsync("TestSchema", ["field1"], [[4]]);
            }

            using (var database = Database.Open(databasePath))
            {
                var deleted = await database.DeleteAsync("TestSchema",
                    [new QueryFilter("field1", QueryFilterOperator.EqualTo, 4)]);
                Assert.That(deleted, Is.EqualTo(1));
            }
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
