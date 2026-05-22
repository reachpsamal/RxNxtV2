namespace Rxnxt.Web.ViewModels;

public sealed class ItemWiseFilterViewModel
{
    public DateTime From { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime To { get; set; } = DateTime.Today;
    public string? Manufacturer { get; set; }
    public string? Batch { get; set; }
    public DateTime? ExpiryFrom { get; set; }
    public DateTime? ExpiryTo { get; set; }
    public string? MovementType { get; set; }
    public decimal? MovementThreshold { get; set; } = 10m;
    public string? StoreId { get; set; }
    public string? CreatedBy { get; set; }
    public string BillStatus { get; set; } = "All";
    public List<string> StoreOptions { get; set; } = new();
    public List<string> UserOptions { get; set; } = new();
}

public sealed class ItemWiseRowViewModel
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Batch { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public decimal QtySold { get; set; }
    public decimal FreeQty { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal SaleValue { get; set; }
    public decimal Profit { get; set; }
    public decimal MarginPerc { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal SalePrice { get; set; }
    public decimal MRP { get; set; }
}

public sealed class DeadStockViewModel
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Batch { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
}

public sealed class ItemWiseReportViewModel
{
    public ItemWiseFilterViewModel Filter { get; set; } = new();
    public List<ItemWiseRowViewModel> Rows { get; set; } = new();
    public List<ItemWiseRowViewModel> Top20 { get; set; } = new();
    public List<DeadStockViewModel> DeadStockItems { get; set; } = new();
    public int NearExpiryCount { get; set; }
    public List<ItemWiseRowViewModel> NearExpiryItems { get; set; } = new();
    public int AbcA { get; set; }
    public int AbcB { get; set; }
    public int AbcC { get; set; }
}
