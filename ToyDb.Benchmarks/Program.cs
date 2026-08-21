using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ToyDb;

BenchmarkRunner.Run<DatabaseBenchmarks>();

[MemoryDiagnoser]
public class DatabaseBenchmarks
{
    private string _databasePath = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.toydb");

        await Database.InitializeAsync(_databasePath);

        using var database = Database.Open(_databasePath);
        await database.AddSchemaAsync(new Schema("Numbers")
            .AddField("Value", SchemaFieldType.Integer, sizeof(int)));
        await database.InsertAsync("Numbers", ["Value"], [[42]]);
    }

    [Benchmark]
    public void OpenDatabase()
    {
        using var database = Database.Open(_databasePath);
    }

    [Benchmark]
    public async Task<int> OpenAndSelectSingleRow()
    {
        using var database = Database.Open(_databasePath);

        var rowCount = 0;
        await foreach (var _ in database.SelectAsync("Numbers", ["Value"]))
        {
            rowCount++;
        }

        return rowCount;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        File.Delete(_databasePath);
    }
}
