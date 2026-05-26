using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Rxnxt.Business.Interfaces;

namespace Rxnxt.Services.Implementations;

public sealed class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public TenantProvider(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public string GetTenantId()
    {
        var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("TenantID")?.Value;
        if (!string.IsNullOrWhiteSpace(claim))
            return claim;

        return _configuration["SalesIntegration:TenantId"] ?? string.Empty;
    }
}
