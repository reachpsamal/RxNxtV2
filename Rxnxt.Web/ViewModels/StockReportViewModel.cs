using Rxnxt.Business.Data;

namespace Rxnxt.Web.ViewModels;

public sealed class StockReportFilterViewModel
{
    public string? Search { get; set; }
    public string? BatchNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string ExpiryStatus { get; set; } = "All";
    public string QuantityStatus { get; set; } = "All";
}

public sealed class StockReportViewModel
{
    public StockReportFilterViewModel Filter { get; set; } = new();
    public List<StockReportRowViewModel> Rows { get; set; } = new();

    public int TotalBatches => Rows.Count;
    public int InStockBatches => Rows.Count(r => r.AvailableQty > 0);
    public int LowStockBatches => Rows.Count(r => r.IsLowStock);
    public int NearExpiryBatches => Rows.Count(r => r.IsNearExpiry);
    public int ExpiredBatches => Rows.Count(r => r.IsExpired);
}

public sealed class StockReportRowViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ManufacturerName { get; set; } = string.Empty;
    public string TaxName { get; set; } = string.Empty;
    public decimal TaxPerc { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal SalePrice { get; set; }
    public string UomName { get; set; } = string.Empty;

    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;
    public bool IsNearExpiry => ExpiryDate.HasValue && !IsExpired && ExpiryDate.Value.Date <= DateTime.Today.AddDays(90);
    public bool IsOutOfStock => AvailableQty <= 0;
    public bool IsLowStock => AvailableQty > 0 && AvailableQty <= 10;

    public static StockReportRowViewModel FromRow(ProductStockViewRow row)
    {
        return new StockReportRowViewModel
        {
            ProductName = row.ProductName,
            ProductId = row.ProductID,
            ManufacturerName = row.ManufacturerName,
            TaxName = row.TaxName ?? string.Empty,
            TaxPerc = row.TaxPerc ?? 0m,
            BatchNumber = row.BatchNumber ?? string.Empty,
            ExpiryDate = row.ExpiryDate,
            Mrp = row.MRP ?? 0m,
            PurchasePrice = row.PurchasePrice ?? 0m,
            AvailableQty = row.AvailableQty ?? 0m,
            SalePrice = row.SalePrice ?? 0m,
            UomName = row.UOMName
        };
    }
}
