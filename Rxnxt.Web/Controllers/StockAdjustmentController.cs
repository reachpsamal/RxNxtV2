using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;
using Rxnxt.Business.Helpers;
using Rxnxt.Business.Interfaces;
using Rxnxt.Services.Implementations;

namespace Rxnxt.Web.Controllers;

[Authorize]
public sealed class StockAdjustmentController : Controller
{
    private readonly PharmacyDbContext _db;
    private readonly StockService _stockService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _configuration;

    public StockAdjustmentController(PharmacyDbContext db, StockService stockService, ITenantProvider tenantProvider, IConfiguration configuration)
    {
        _db = db;
        _stockService = stockService;
        _tenantProvider = tenantProvider;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        var movements = await _db.StockMovements
            .Where(m => m.MovementType == "Adjustment")
            .OrderByDescending(m => m.CreatedDate)
            .Take(50)
            .ToListAsync();

        var productIds = movements.Select(m => m.ProductID).Distinct().ToList();
        var products = await _db.ProductMasters
            .Where(p => productIds.Contains(p.UniqueID))
            .Select(p => new { p.UniqueID, p.ProductName })
            .ToListAsync();
        var productLookup = products.ToDictionary(p => p.UniqueID, p => p.ProductName ?? p.UniqueID);

        ViewBag.ProductLookup = productLookup;
        return View(movements);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string productId, string? batchNumber, DateTime? expiryDate, decimal quantity, string? remarks)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            TempData["AdjustmentError"] = "Product is required.";
            return RedirectToAction("Index");
        }

        if (quantity == 0)
        {
            TempData["AdjustmentError"] = "Quantity must be non-zero.";
            return RedirectToAction("Index");
        }

        var tenantId = _tenantProvider.GetTenantId();
        var createdBy = _configuration["SalesIntegration:CreatedBy"] ?? "POS";

        var direction = quantity > 0 ? "Inward" : "Outward";

        var batchNorm = (batchNumber ?? string.Empty).Trim();

        var stockRow = await _db.ProductStocks.FirstOrDefaultAsync(ps =>
            ps.ProductID == productId &&
            (ps.BatchNumber ?? string.Empty) == batchNorm &&
            (!expiryDate.HasValue || (ps.ExpiryDate.HasValue && ps.ExpiryDate.Value.Date == expiryDate.Value.Date)));

        var openingBalance = stockRow?.PackQty ?? 0m;

        if (stockRow == null)
        {
            if (quantity < 0)
            {
                TempData["AdjustmentError"] = "Cannot deduct from non-existent stock.";
                return RedirectToAction("Index");
            }

            stockRow = new ProductStockRow
            {
                ProductID = productId,
                BatchNumber = string.IsNullOrWhiteSpace(batchNorm) ? null : batchNorm,
                ExpiryDate = expiryDate?.Date,
                PackQty = quantity
            };
            _db.ProductStocks.Add(stockRow);
            await _db.SaveChangesAsync();
        }
        else
        {
            var newQty = (stockRow.PackQty ?? 0m) + quantity;
            if (newQty < 0)
            {
                TempData["AdjustmentError"] = $"Insufficient stock. Available: {stockRow.PackQty ?? 0m:0.##}, attempted change: {quantity:0.##}";
                return RedirectToAction("Index");
            }

            stockRow.PackQty = newQty;
        }

        var movement = StockMovementHelper.BuildMovement(
            productID: productId,
            productStockID: stockRow.ID > 0 ? stockRow.ID : null,
            batchNumber: batchNorm,
            expiryDate: expiryDate?.Date,
            openingBalance: openingBalance,
            baseQtyDelta: quantity,
            direction: direction,
            movementType: "Adjustment",
            transactionQty: Math.Abs(quantity),
            referenceType: "StockAdjustment",
            referenceID: null,
            referenceNo: null,
            remarks: remarks,
            tenantId: tenantId,
            createdBy: createdBy);

        _db.StockMovements.Add(movement);
        await _db.SaveChangesAsync();

        TempData["AdjustmentSuccess"] = direction == "Inward"
            ? $"Added {quantity:0.##} to stock successfully."
            : $"Removed {Math.Abs(quantity):0.##} from stock successfully.";

        return RedirectToAction("Index");
    }
}
