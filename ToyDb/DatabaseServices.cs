using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

namespace ToyDb;

internal static class DatabaseServices
{
    // Each database owns a provider, so singleton state is isolated to that database.
    public static ServiceProvider Create(string filePath, DatabaseConfig? config = null)
    {
        config ??= new DatabaseConfig { FrameCount = 20 };
        var services = new ServiceCollection();
        services.AddLogging(builder =>
            builder.AddSimpleConsole(options => options.SingleLine = true));
        services.AddSingleton(new PageBufferConfig(FrameCount: config.FrameCount));
        services.AddSingleton(provider =>
            new FileIoManager(filePath, provider.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<PageBufferManager>();
        services.AddSingleton<DatabaseManager>();
        services.AddSingleton<SchemaManager>();
        services.AddSingleton<ExecutionEngine>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
