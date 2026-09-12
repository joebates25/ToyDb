namespace ToyDb.Tests;

public class DatabaseTests
{
    [Test]
    public async Task CanInitializeAndOpenDatabase()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");

        try
        {
            using var database = await Database.OpenAsync(databasePath);

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
            using var database = await Database.OpenAsync(databasePath);
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
            using (var database = await Database.OpenAsync(databasePath))
            {
                await database.AddSchemaAsync(new Schema("TestSchema")
                    .AddField("field1", SchemaFieldType.Integer, 4));
            }

            using (var database = await Database.OpenAsync(databasePath))
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
        // Get database with schema
        Database testDatabase = null;

        try
        {
            testDatabase = await (await GetTestDatabaseAsync()).WithTestSchema(schemaName: "TestSchema");
            // Insert tons of data
            await testDatabase.InsertAsync("TestSchema", ["field1"],
                Enumerable.Range(0, 50000).Select(i => new object[] {i}).ToArray());
            // Get n row and verify it is correct
            var results = await testDatabase.SelectAsync("TestSchema", ["field1"]).ToListAsync();
            Assert.That(results.Count, Is.EqualTo(50000));
            Assert.That(results[0][0], Is.EqualTo(0));
            await testDatabase.CloseAsync();
        }
        finally
        {
            CleanUpDatabase(testDatabase);
        }
    }

    private static void CleanUpDatabase(Database? testDatabase)
    {
        if (testDatabase != null) File.Delete(testDatabase.Info.FilePath);
    }

    private async Task<Database> GetTestDatabaseAsync()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");
        var database = await Database.OpenAsync(databasePath);
        return database;
    }

    [Test]
    public async Task CanInsertAndDeleteData()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");

        try
        {
            using (var database = await Database.OpenAsync(databasePath))
            {
                await database.AddSchemaAsync(new Schema("TestSchema")
                    .AddField("field1", SchemaFieldType.Integer, 4));
                await database.InsertAsync("TestSchema", ["field1"], [[4]]);
            }

            using (var database = await Database.OpenAsync(databasePath))
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

    [Test]
    public async Task CanContinueInsertingAfterDataPageOverflowAndReopen()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");

        try
        {
            using (var database = await Database.OpenAsync(databasePath))
            {
                await database.AddSchemaAsync(new Schema("TestSchema")
                    .AddField("field1", SchemaFieldType.String, 255));

                var values = Enumerable.Range(0, 20)
                    .Select(value => new object[] {value.ToString()})
                    .ToArray();

                await database.InsertAsync("TestSchema", ["field1"], values);
                await database.InsertAsync("TestSchema", ["field1"], [["same session"]]);
            }

            using (var database = await Database.OpenAsync(databasePath))
            {
                await database.InsertAsync("TestSchema", ["field1"], [["reopened"]]);

                var results = await database.SelectAsync("TestSchema", ["field1"]).ToListAsync();
                Assert.That(results, Has.Count.EqualTo(22));
            }
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}

public static class TestDatabaseExtensions
{
    public static Task<Database> WithTestSchema(this Database database, string schemaName)
    {
        return database.AddSchemaAsync(new Schema(schemaName)
                .AddField("field1", SchemaFieldType.Integer, 4))
            .ContinueWith(_ => database);
    }
}