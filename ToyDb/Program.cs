using ToyDb;

var dbLocation = Path.GetFullPath("toy-store-demo.toydb");
var resetRequested = args.Contains("--reset", StringComparer.OrdinalIgnoreCase);

if (resetRequested && File.Exists(dbLocation))
{
    File.Delete(dbLocation);
}

Console.WriteLine("ToyDb store demo");
Console.WriteLine($"Database: {dbLocation}");

if (!File.Exists(dbLocation))
{
    await CreateAndSeedDatabaseAsync(dbLocation);
}
else
{
    Console.WriteLine("Using the existing database. Pass --reset to rebuild the sample data.");
}

using var database = Database.Open(dbLocation);

await PrintRowsAsync(
    database,
    tableName: "Customers",
    columns: ["Id", "FullName", "City", "LoyaltyPoints", "IsActive"],
    maximumRows: 8,
    format: row =>
        $"#{row[0],4}  {row[1],-24}  {row[2],-14}  points: {row[3],5}  active: {row[4]}");

await PrintRowsAsync(
    database,
    tableName: "Products",
    columns: ["Sku", "Name", "Category", "PriceInCents", "UnitsInStock"],
    maximumRows: 10,
    format: row =>
        $"{row[0],-10}  {row[1],-35}  {row[2],-16}  {FormatMoney((long) row[3]),9}  stock: {row[4]}");

Console.WriteLine("\nRecent completed orders (filtered by the caller):");
var displayedOrders = 0;
await foreach (var row in database.SelectAsync(
                   "Orders",
                   ["Id", "CustomerId", "PlacedAtUnixSeconds", "TotalInCents", "IsComplete"]))
{
    if (!(bool) row[4])
    {
        continue;
    }

    var placedAt = DateTimeOffset.FromUnixTimeSeconds((long) row[2]);
    Console.WriteLine(
        $"Order #{row[0]}  customer #{row[1]}  {placedAt:yyyy-MM-dd}  {FormatMoney((long) row[3])}");

    if (++displayedOrders == 8)
    {
        break;
    }
}

var customerCount = await CountRowsAsync(database, "Customers", "Id");
var productCount = await CountRowsAsync(database, "Products", "Id");
var orderCount = await CountRowsAsync(database, "Orders", "Id");

Console.WriteLine($"\nTotals read through projected selects: {customerCount} customers, " +
                  $"{productCount} products, {orderCount} orders.");

await database.CloseAsync();

static async Task CreateAndSeedDatabaseAsync(string dbLocation)
{
    Console.WriteLine("Creating schemas and inserting realistic sample data...");

    await Database.InitializeAsync(dbLocation);
    using var database = Database.Open(dbLocation);

    await database.AddSchemaAsync(
        new Schema("Customers")
            .AddField("Id", SchemaFieldType.Long, sizeof(long))
            .AddField("FullName", SchemaFieldType.String, 80)
            .AddField("Email", SchemaFieldType.String, 120)
            .AddField("City", SchemaFieldType.String, 60)
            .AddField("LoyaltyPoints", SchemaFieldType.Integer, sizeof(int))
            .AddField("IsActive", SchemaFieldType.Boolean, sizeof(byte)));

    await database.AddSchemaAsync(
        new Schema("Products")
            .AddField("Id", SchemaFieldType.Long, sizeof(long))
            .AddField("Sku", SchemaFieldType.String, 24)
            .AddField("Name", SchemaFieldType.String, 100)
            .AddField("Category", SchemaFieldType.String, 50)
            .AddField("PriceInCents", SchemaFieldType.Long, sizeof(long))
            .AddField("UnitsInStock", SchemaFieldType.Integer, sizeof(int))
            .AddField("IsActive", SchemaFieldType.Boolean, sizeof(byte)));

    await database.AddSchemaAsync(
        new Schema("Orders")
            .AddField("Id", SchemaFieldType.Long, sizeof(long))
            .AddField("CustomerId", SchemaFieldType.Long, sizeof(long))
            .AddField("PlacedAtUnixSeconds", SchemaFieldType.Long, sizeof(long))
            .AddField("TotalInCents", SchemaFieldType.Long, sizeof(long))
            .AddField("ItemCount", SchemaFieldType.Integer, sizeof(int))
            .AddField("IsComplete", SchemaFieldType.Boolean, sizeof(byte)));

    var customerRows = BuildCustomerRows(120);
    var productRows = BuildProductRows();
    var orderRows = BuildOrderRows(240, customerRows.Length);

    var insertedCustomers = await database.InsertAsync(
        "Customers",
        ["Id", "FullName", "Email", "City", "LoyaltyPoints", "IsActive"],
        customerRows);

    var insertedProducts = await database.InsertAsync(
        "Products",
        ["Id", "Sku", "Name", "Category", "PriceInCents", "UnitsInStock", "IsActive"],
        productRows);

    var insertedOrders = await database.InsertAsync(
        "Orders",
        ["Id", "CustomerId", "PlacedAtUnixSeconds", "TotalInCents", "ItemCount", "IsComplete"],
        orderRows);

    await database.CloseAsync();

    Console.WriteLine(
        $"Inserted {insertedCustomers} customers, {insertedProducts} products, and {insertedOrders} orders.");
}

static object[][] BuildCustomerRows(int count)
{
    string[] firstNames =
    [
        "Avery", "Maya", "Theo", "Sofia", "Miles", "Nora", "Elliot", "Zoe",
        "Caleb", "Layla", "Jonah", "Iris", "Liam", "Amara", "Felix", "Elena"
    ];
    string[] lastNames =
    [
        "Nguyen", "Patel", "Johnson", "Garcia", "Kim", "Anderson", "Martinez", "Brown",
        "Wilson", "Davis", "Clark", "Lewis", "Walker", "Hall", "Young", "King"
    ];
    string[] cities =
    [
        "Chicago", "Austin", "Seattle", "Denver", "Boston", "Atlanta",
        "Portland", "Nashville", "Madison", "Minneapolis"
    ];
    string[] emailDomains = ["example.com", "sample.org", "demo.net"];

    return Enumerable.Range(0, count)
        .Select(index =>
        {
            var firstName = firstNames[index % firstNames.Length];
            var lastName = lastNames[index * 7 % lastNames.Length];
            var email = $"{firstName}.{lastName}{index + 1}@{emailDomains[index % emailDomains.Length]}"
                .ToLowerInvariant();

            return new object[]
            {
                1_001L + index,
                $"{firstName} {lastName}",
                email,
                cities[index * 3 % cities.Length],
                75 + index * 137 % 8_000,
                index % 11 != 0
            };
        })
        .ToArray();
}

static object[][] BuildProductRows()
{
    (string Name, string Category, long PriceInCents, int Stock)[] catalog =
    [
        ("Pour-Over Coffee Kettle", "Kitchen", 7_900, 38),
        ("Walnut Cutting Board", "Kitchen", 6_500, 24),
        ("Linen Table Runner", "Home", 3_800, 51),
        ("Ceramic Desk Planter", "Home", 2_600, 74),
        ("Adjustable Task Lamp", "Office", 8_900, 31),
        ("Recycled Paper Notebook", "Office", 1_800, 140),
        ("Mechanical Keyboard", "Electronics", 12_900, 19),
        ("USB-C Charging Hub", "Electronics", 7_400, 46),
        ("Noise-Isolating Earbuds", "Electronics", 5_900, 62),
        ("Merino Wool Beanie", "Apparel", 4_200, 55),
        ("Canvas Weekend Bag", "Apparel", 9_800, 17),
        ("Stainless Water Bottle", "Outdoors", 3_200, 88),
        ("Compact Camp Lantern", "Outdoors", 4_900, 43),
        ("Resistance Band Set", "Fitness", 2_900, 67),
        ("Cork Yoga Mat", "Fitness", 7_800, 29),
        ("Travel Chess Set", "Games", 3_600, 35)
    ];
    string[] editions = ["Standard", "Plus", "Pro"];

    return Enumerable.Range(0, catalog.Length * editions.Length)
        .Select(index =>
        {
            var product = catalog[index % catalog.Length];
            var edition = index / catalog.Length;

            return new object[]
            {
                2_001L + index,
                $"SKU-{index + 1:0000}",
                $"{product.Name} - {editions[edition]}",
                product.Category,
                product.PriceInCents + edition * 1_250L,
                Math.Max(0, product.Stock - edition * 9),
                index % 13 != 0
            };
        })
        .ToArray();
}

static object[][] BuildOrderRows(int count, int customerCount)
{
    var firstOrderDate = new DateTimeOffset(2025, 1, 3, 9, 0, 0, TimeSpan.Zero);

    return Enumerable.Range(0, count)
        .Select(index => new object[]
        {
            10_001L + index,
            1_001L + index * 17 % customerCount,
            firstOrderDate.AddHours(index * 11).ToUnixTimeSeconds(),
            1_800L + index * 791L % 40_000L,
            1 + index % 6,
            index % 7 != 0
        })
        .ToArray();
}

static async Task PrintRowsAsync(
    Database database,
    string tableName,
    string[] columns,
    int maximumRows,
    Func<object[], string> format)
{
    Console.WriteLine($"\n{tableName} sample:");

    var displayedRows = 0;
    await foreach (var row in database.SelectAsync(tableName, columns,
                   [
                       new QueryFilter("Column", QueryFilterOperator.EqualTo, 3),
                       new QueryFilter("AnotherColumn", QueryFilterOperator.LessThan, "Value")
                   ]))
    {
        Console.WriteLine(format(row));
        if (++displayedRows == maximumRows)
        {
            break;
        }
    }
}

static async Task<int> CountRowsAsync(Database database, string tableName, string identityColumn)
{
    var count = 0;
    await foreach (var _ in database.SelectAsync(tableName, [identityColumn]))
    {
        count++;
    }

    return count;
}

static string FormatMoney(long cents) => $"{cents / 100m:C}";