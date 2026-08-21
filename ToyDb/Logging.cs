using Microsoft.Extensions.Logging;

namespace ToyDb;

public static class Logging
{
    public static readonly ILoggerFactory LoggerFactory =
        Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddSimpleConsole(options => { options.SingleLine = true; }));
}