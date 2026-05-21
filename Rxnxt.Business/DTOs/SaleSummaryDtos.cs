namespace Rxnxt.Business.DTOs;

public sealed class SaleSummaryRequest
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? StoreId { get; set; }
    public string? CreatedBy { get; set; }
    public string? PaymentMode { get; set; }
    public string BillStatus { get; set; } = "All";
    public string GroupBy { get; set; } = "Day";
}

public sealed class SaleSummaryRowDto
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

public sealed class SaleSummaryKpiDto
{
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
