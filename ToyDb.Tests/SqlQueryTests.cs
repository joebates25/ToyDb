namespace ToyDb.Tests;

public class SqlQueryTests
{
    [Test]
    public async Task ExecuteSqlQuery_ParsesProjectsAndFiltersRows()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");

        try
        {
            using var database = await Database.OpenAsync(databasePath);
            await database.AddSchemaAsync(
                new Schema("Products")
                    .AddField("Id", SchemaFieldType.Long, sizeof(long))
                    .AddField("Stock", SchemaFieldType.Integer, sizeof(int))
                    .AddField("Active", SchemaFieldType.Boolean, sizeof(byte)));
            await database.InsertAsync(
                "Products",
                ["Id", "Stock", "Active"],
                [[1L, 10, true], [2L, 20, false], [3L, 30, true]]);

            var rows = await database.ExecuteSqlQuery(
                    "SELECT Id, Stock FROM Products WHERE Id > 1 AND Active = true")
                .ToListAsync();

            Assert.That(rows, Is.EqualTo(new[] { new object[] { 3L, 30 } }));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Test]
    public async Task ExecuteSqlQuery_WildcardProjectsAllColumns()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid()}.toydb");

        try
        {
            using var database = await Database.OpenAsync(databasePath);
            await database.AddSchemaAsync(
                new Schema("Items")
                    .AddField("Id", SchemaFieldType.Integer, sizeof(int))
                    .AddField("Name", SchemaFieldType.String, 20));
            await database.InsertAsync("Items", ["Id", "Name"], [[7, "Wrench"]]);

            var rows = await database.ExecuteSqlQuery("SELECT * FROM Items").ToListAsync();

            Assert.That(rows, Is.EqualTo(new[] { new object[] { 7, "Wrench" } }));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
