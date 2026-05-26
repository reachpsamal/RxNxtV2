using Microsoft.Extensions.DependencyInjection;
using Rxnxt.Business.Interfaces;
using Rxnxt.Services.Implementations;

namespace Rxnxt.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRxnxtServices(this IServiceCollection services)
    {
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<CustomerService>();
        services.AddScoped<SaleService>();
        services.AddScoped<AuthService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
