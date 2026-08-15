using ToyDb;

var dbLocation = @"C:/users/josephbates/file.toydb";

Console.WriteLine("Hello, ToyDb!");

if (!File.Exists(dbLocation))
{
    await Database.InitializeAsync(dbLocation);
    var database = Database.Open(dbLocation);
    var schema = new Schema("MySchema")
        .AddField("Name", SchemaFieldType.String, 10)
        .AddField("Count", SchemaFieldType.Integer, 4)
        .AddField("IsEnabled", SchemaFieldType.Boolean, 1);
    await database.AddSchemaAsync(schema);

    var sampleSchemas = new[]
    {
        new Schema("Customers")
            .AddField("Name", SchemaFieldType.String, 80)
            .AddField("Email", SchemaFieldType.String, 120)
            .AddField("LoyaltyPoints", SchemaFieldType.Integer, 4)
            .AddField("IsActive", SchemaFieldType.Boolean, 1),
        new Schema("Products")
            .AddField("Name", SchemaFieldType.String, 100)
            .AddField("Sku", SchemaFieldType.String, 32)
            .AddField("PriceInCents", SchemaFieldType.Long, 8)
            .AddField("InStock", SchemaFieldType.Boolean, 1),
        new Schema("Orders")
            .AddField("CustomerId", SchemaFieldType.Long, 8)
            .AddField("CreatedAt", SchemaFieldType.Long, 8)
            .AddField("TotalInCents", SchemaFieldType.Long, 8)
            .AddField("IsComplete", SchemaFieldType.Boolean, 1),
        new Schema("OrderItems")
            .AddField("OrderId", SchemaFieldType.Long, 8)
            .AddField("ProductId", SchemaFieldType.Long, 8)
            .AddField("Quantity", SchemaFieldType.Integer, 4)
            .AddField("UnitPriceInCents", SchemaFieldType.Long, 8),
        new Schema("Suppliers")
            .AddField("Name", SchemaFieldType.String, 100)
            .AddField("ContactName", SchemaFieldType.String, 80)
            .AddField("Phone", SchemaFieldType.String, 24)
            .AddField("IsPreferred", SchemaFieldType.Boolean, 1),
        new Schema("Inventory")
            .AddField("ProductId", SchemaFieldType.Long, 8)
            .AddField("WarehouseId", SchemaFieldType.Long, 8)
            .AddField("Quantity", SchemaFieldType.Integer, 4)
            .AddField("ReorderLevel", SchemaFieldType.Integer, 4),
        new Schema("Employees")
            .AddField("FirstName", SchemaFieldType.String, 50)
            .AddField("LastName", SchemaFieldType.String, 50)
            .AddField("DepartmentId", SchemaFieldType.Long, 8)
            .AddField("IsManager", SchemaFieldType.Boolean, 1),
        new Schema("Departments")
            .AddField("Name", SchemaFieldType.String, 80)
            .AddField("ManagerId", SchemaFieldType.Long, 8)
            .AddField("CostCenter", SchemaFieldType.Integer, 4),
        new Schema("Addresses")
            .AddField("CustomerId", SchemaFieldType.Long, 8)
            .AddField("Street", SchemaFieldType.String, 120)
            .AddField("City", SchemaFieldType.String, 60)
            .AddField("PostalCode", SchemaFieldType.String, 20),
        new Schema("Payments")
            .AddField("OrderId", SchemaFieldType.Long, 8)
            .AddField("AmountInCents", SchemaFieldType.Long, 8)
            .AddField("Provider", SchemaFieldType.String, 40)
            .AddField("Succeeded", SchemaFieldType.Boolean, 1),
        new Schema("Shipments")
            .AddField("OrderId", SchemaFieldType.Long, 8)
            .AddField("TrackingNumber", SchemaFieldType.String, 64)
            .AddField("Carrier", SchemaFieldType.String, 40)
            .AddField("ShippedAt", SchemaFieldType.Long, 8),
        new Schema("Categories")
            .AddField("Name", SchemaFieldType.String, 80)
            .AddField("ParentCategoryId", SchemaFieldType.Long, 8)
            .AddField("DisplayOrder", SchemaFieldType.Integer, 4),
        new Schema("Reviews")
            .AddField("ProductId", SchemaFieldType.Long, 8)
            .AddField("CustomerId", SchemaFieldType.Long, 8)
            .AddField("Rating", SchemaFieldType.Integer, 4)
            .AddField("Comment", SchemaFieldType.String, 200),
        new Schema("Coupons")
            .AddField("Code", SchemaFieldType.String, 32)
            .AddField("DiscountPercent", SchemaFieldType.Integer, 4)
            .AddField("ExpiresAt", SchemaFieldType.Long, 8)
            .AddField("IsEnabled", SchemaFieldType.Boolean, 1),
        new Schema("Warehouses")
            .AddField("Name", SchemaFieldType.String, 80)
            .AddField("Region", SchemaFieldType.String, 40)
            .AddField("Capacity", SchemaFieldType.Integer, 4)
            .AddField("IsOperational", SchemaFieldType.Boolean, 1),
        new Schema("AuditLogs")
            .AddField("EntityName", SchemaFieldType.String, 80)
            .AddField("EntityId", SchemaFieldType.Long, 8)
            .AddField("Action", SchemaFieldType.String, 32)
            .AddField("OccurredAt", SchemaFieldType.Long, 8),
        new Schema("Sessions")
            .AddField("CustomerId", SchemaFieldType.Long, 8)
            .AddField("Token", SchemaFieldType.String, 128)
            .AddField("ExpiresAt", SchemaFieldType.Long, 8)
            .AddField("IsRevoked", SchemaFieldType.Boolean, 1),
        new Schema("Settings")
            .AddField("Key", SchemaFieldType.String, 80)
            .AddField("Value", SchemaFieldType.String, 200)
            .AddField("IsSecret", SchemaFieldType.Boolean, 1),
        new Schema("Notifications")
            .AddField("CustomerId", SchemaFieldType.Long, 8)
            .AddField("Message", SchemaFieldType.String, 200)
            .AddField("CreatedAt", SchemaFieldType.Long, 8)
            .AddField("IsRead", SchemaFieldType.Boolean, 1),
        new Schema("Tags")
            .AddField("Name", SchemaFieldType.String, 50)
            .AddField("UsageCount", SchemaFieldType.Integer, 4)
            .AddField("IsVisible", SchemaFieldType.Boolean, 1)
    };

    foreach (var sampleSchema in sampleSchemas)
    {
        await database.AddSchemaAsync(sampleSchema);
    }

    await database.CloseAsync();
}

var db = Database.Open(dbLocation);
