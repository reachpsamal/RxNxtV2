using Microsoft.Extensions.DependencyInjection;
using Rxnxt.Logging.Implementations;
using Rxnxt.Logging.Interfaces;

namespace Rxnxt.Logging;

public static class LoggerFactory
{
    public static IServiceCollection AddRxnxtLogging(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<IAppLoggerFactory, AppLoggerFactory>();
        return services;
    }
}
