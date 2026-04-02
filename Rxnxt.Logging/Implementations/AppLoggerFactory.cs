using Microsoft.Extensions.Logging;
using Rxnxt.Logging.Interfaces;

namespace Rxnxt.Logging.Implementations;

internal sealed class AppLoggerFactory : IAppLoggerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public AppLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IAppLogger CreateLogger(string categoryName) =>
        new AppLogger(_loggerFactory.CreateLogger(categoryName));

    public IAppLogger CreateLogger<TCategoryName>() =>
        new AppLogger(_loggerFactory.CreateLogger<TCategoryName>());
}
