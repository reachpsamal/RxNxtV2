using System;
using Microsoft.Extensions.Logging;
using Rxnxt.Logging.Interfaces;

namespace Rxnxt.Logging.Implementations;

internal sealed class AppLogger : IAppLogger
{
    private readonly ILogger _logger;

    public AppLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void LogInformation(string message) => _logger.LogInformation("{Message}", message);

    public void LogWarning(string message) => _logger.LogWarning("{Message}", message);

    public void LogError(string message, Exception? exception = null)
    {
        if (exception == null)
        {
            _logger.LogError("{Message}", message);
            return;
        }

        _logger.LogError(exception, "{Message}", message);
    }
}
