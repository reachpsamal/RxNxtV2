namespace Rxnxt.Web.ViewModels;

public sealed class SaleSummaryFilterViewModel
{
    public DateTime From { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime To { get; set; } = DateTime.Today;
    public string? StoreId { get; set; }
    public string? CreatedBy { get; set; }
    public string? PaymentMode { get; set; }
    public string BillStatus { get; set; } = "All";
    public string GroupBy { get; set; } = "Day";

    public List<string> StoreOptions { get; set; } = new();
    public List<string> UserOptions { get; set; } = new();
}

public sealed class SaleSummaryRowViewModel
{
    public string GroupKey { get; set; } = string.Empty;
    public int BillCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal RoundOff { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal Outstanding { get; set; }
}

public sealed class SaleSummaryViewModel
{
    public SaleSummaryFilterViewModel Filter { get; set; } = new();
    public List<SaleSummaryRowViewModel> Rows { get; set; } = new();

    public decimal AvgBillValue { get; set; }
    public int TotalBills { get; set; }
    public decimal CashAmount { get; set; }
    public decimal UpiAmount { get; set; }
    public decimal CardAmount { get; set; }
    public decimal OtherAmount { get; set; }
    public decimal ReturnPercentage { get; set; }
    public decimal TotalGrossSales { get; set; }
    public decimal TotalRefunds { get; set; }
}
