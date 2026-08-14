using ToyDb;

var dbLocation = @"C:/users/josephbates/file.toydb";

Console.WriteLine("Hello, ToyDb!");

if (!File.Exists(dbLocation))
{
    await Database.InitializeAsync(dbLocation);
}

var database = Database.Open(dbLocation);
var schema = new Schema("MySchema")
    .AddField("Name", SchemaFieldType.String, 10)
    .AddField("Count", SchemaFieldType.Integer, 4)
    .AddField("IsEnabled", SchemaFieldType.Boolean, 1);
await database.AddSchemaAsync(schema);