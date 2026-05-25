using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;
using Rxnxt.Services.Implementations;
using Rxnxt.Web.ViewModels;

namespace Rxnxt.Web.Controllers;

[Authorize]
public sealed class StockController : Controller
{
    private readonly PharmacyDbContext _db;
    private readonly StockService _stockService;
    private readonly IConfiguration _configuration;

    public StockController(PharmacyDbContext db, StockService stockService, IConfiguration configuration)
    {
        _db = db;
        _stockService = stockService;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] string? search,
        [FromQuery] string? batchNumber,
        [FromQuery] string? manufacturer,
        [FromQuery] string? expiryStatus,
        [FromQuery] string? quantityStatus,
        CancellationToken cancellationToken)
    {
        var filter = new StockReportFilterViewModel
        {
            Search = search,
            BatchNumber = batchNumber,
            Manufacturer = manufacturer,
            ExpiryStatus = NormalizeOption(expiryStatus),
            QuantityStatus = NormalizeOption(quantityStatus)
        };

        var query = _db.ProductStockView.AsNoTracking();

        var tenantId = _configuration["ExternalApis:ArogyaStocks:TenantId"];
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            query = query.Where(s => s.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            //query = query.Where(s => s.ProductName.ToLower().Contains(term) || s.ProductID.ToLower().Contains(term));
            query = query.Where(s => s.ProductName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.BatchNumber))
        {
            var batch = filter.BatchNumber.Trim().ToLowerInvariant();
            query = query.Where(s => s.BatchNumber != null && s.BatchNumber.ToLower().Contains(batch));
        }

        if (!string.IsNullOrWhiteSpace(filter.Manufacturer))
        {
            var maker = filter.Manufacturer.Trim().ToLowerInvariant();
            query = query.Where(s => s.ManufacturerName.ToLower().Contains(maker));
        }

        var today = DateTime.Today;
        var nearExpiryUntil = today.AddDays(90);

        query = filter.ExpiryStatus switch
        {
            "Available" => query.Where(s => !s.ExpiryDate.HasValue || s.ExpiryDate.Value.Date >= today),
            "NearExpiry" => query.Where(s => s.ExpiryDate.HasValue && s.ExpiryDate.Value.Date >= today && s.ExpiryDate.Value.Date <= nearExpiryUntil),
            "Expired" => query.Where(s => s.ExpiryDate.HasValue && s.ExpiryDate.Value.Date < today),
            _ => query
        };

        query = filter.QuantityStatus switch
        {
            "InStock" => query.Where(s => (s.AvailableQty ?? 0m) > 0m),
            "LowStock" => query.Where(s => (s.AvailableQty ?? 0m) > 0m && (s.AvailableQty ?? 0m) <= 10m),
            "OutOfStock" => query.Where(s => (s.AvailableQty ?? 0m) <= 0m),
            _ => query
        };

        var stockRows = await query
            .OrderBy(s => s.ProductName)
            .ThenBy(s => s.BatchNumber)
            .ThenBy(s => s.ExpiryDate)
            .ToListAsync(cancellationToken);

        return View(new StockReportViewModel
        {
            Filter = filter,
            Rows = stockRows.Select(StockReportRowViewModel.FromRow).ToList()
        });
    }

    [HttpGet("api/stocks")]
    public async Task<IActionResult> GetStocks(CancellationToken cancellationToken)
    {
        var stocks = await _stockService.GetStocksAsync(cancellationToken);
        return Ok(stocks);
    }

    [HttpGet("api/stocks/search")]
    public async Task<IActionResult> Search([FromQuery] string? term, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Ok(Array.Empty<object>());
        }

        var stocks = await _stockService.GetStocksAsync(cancellationToken);
        var result = stocks
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductName) &&
                        x.ProductName.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();

        return Ok(result);
    }

    [HttpGet("api/stocks/batch")]
    public async Task<IActionResult> SearchByBatch([FromQuery] string? batch, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(batch))
        {
            return Ok(Array.Empty<object>());
        }

        var stocks = await _stockService.GetStocksAsync(cancellationToken);
        var result = stocks
            .Where(x => !string.IsNullOrWhiteSpace(x.BatchNumber) &&
                        x.BatchNumber.Contains(batch.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();

        return Ok(result);
    }

    private static string NormalizeOption(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "All" : value.Trim();
    }
}
