using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Rxnxt.Business.DTOs;

namespace Rxnxt.Services.Implementations;

public sealed class StockService
{
    private const string ConfigSectionPath = "ExternalApis:ArogyaStocks";
    private const string TenantHeaderName = "X-Arogya-TenantId";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public StockService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<StockDto>> GetStocksAsync(CancellationToken cancellationToken = default)
    {
        var url = _configuration[$"{ConfigSectionPath}:Url"];
        var tenantId = _configuration[$"{ConfigSectionPath}:TenantId"];

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException($"Missing configuration key '{ConfigSectionPath}:Url'.");
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException($"Missing configuration key '{ConfigSectionPath}:TenantId'.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Stocks API call failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}). Response: {body}");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var payload = await JsonSerializer.DeserializeAsync<StockResponseDto>(
            responseStream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        return payload?.Result ?? new List<StockDto>();
    }

    public async Task<List<StockDto>> SearchBatchAsync(string query, int take = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return new List<StockDto>();
        var q = query.Trim();

        var stocks = await GetStocksAsync(cancellationToken);
        return stocks
            .Where(s => !string.IsNullOrWhiteSpace(s.BatchNumber) && s.BatchNumber.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.BatchNumber)
            .Take(take)
            .ToList();
    }

    public async Task<List<StockDto>> SearchMedicineAsync(string query, int take = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return new List<StockDto>();
        var q = query.Trim();

        var stocks = await GetStocksAsync(cancellationToken);
        return stocks
            .Where(s => !string.IsNullOrWhiteSpace(s.ProductName) && s.ProductName.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.ProductName)
            .ThenBy(s => s.BatchNumber)
            .Take(take)
            .ToList();
    }

    public async Task<StockDto?> GetStockByProductBatchAsync(Guid productId, string batchNumber, CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty || string.IsNullOrWhiteSpace(batchNumber)) return null;
        var bn = batchNumber.Trim();

        var stocks = await GetStocksAsync(cancellationToken);
        return stocks.FirstOrDefault(s => s.ProductId == productId && string.Equals(s.BatchNumber, bn, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<StockDto>> AdvancedBatchSearchAsync(
        string? batchNumber,
        string? medicineName,
        DateTime? expiryFrom,
        DateTime? expiryTo,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var stocks = await GetStocksAsync(cancellationToken);
        IEnumerable<StockDto> query = stocks;

        if (!string.IsNullOrWhiteSpace(batchNumber))
            query = query.Where(s => !string.IsNullOrWhiteSpace(s.BatchNumber) && s.BatchNumber.Contains(batchNumber.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(medicineName))
            query = query.Where(s => !string.IsNullOrWhiteSpace(s.ProductName) && s.ProductName.Contains(medicineName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (expiryFrom.HasValue)
            query = query.Where(s => s.ExpiryDate.HasValue && s.ExpiryDate.Value.Date >= expiryFrom.Value.Date);

        if (expiryTo.HasValue)
            query = query.Where(s => s.ExpiryDate.HasValue && s.ExpiryDate.Value.Date <= expiryTo.Value.Date);

        return query
            .OrderBy(s => s.ProductName)
            .ThenBy(s => s.BatchNumber)
            .Take(take)
            .ToList();
    }
}
