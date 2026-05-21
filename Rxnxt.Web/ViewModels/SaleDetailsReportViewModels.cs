namespace Rxnxt.Web.ViewModels;

public sealed class SaleDetailsReportFilterViewModel
{
    public DateTime From { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime To { get; set; } = DateTime.Today;
    public string? InvoiceNo { get; set; }
    public string? CustomerName { get; set; }
    public string? ItemName { get; set; }
    public string? CreatedBy { get; set; }
    public string? PaymentMode { get; set; }
    public string BillStatus { get; set; } = "All";
    public List<string> UserOptions { get; set; } = new();
}

public sealed class SaleDetailsReportRowViewModel
{
    public string HeaderId { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Batch { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal FreeQty { get; set; }
    public decimal Mrp { get; set; }
    public decimal Rate { get; set; }
    public decimal Discount { get; set; }
    public decimal GstPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string PaymentMode { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsCancelled { get; set; }
}

public sealed class SaleDetailsReportViewModel
{
    public SaleDetailsReportFilterViewModel Filter { get; set; } = new();
    public List<SaleDetailsReportRowViewModel> Rows { get; set; } = new();
}
