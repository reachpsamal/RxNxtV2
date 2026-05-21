namespace Rxnxt.Web.ViewModels;

internal sealed class HeaderAggDto
{
    public string GroupKey { get; set; } = string.Empty;
    public int BillCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal RoundOff { get; set; }
}

internal sealed class PaymentAggDto
{
    public string GroupKey { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
}

internal sealed class RefundAggDto
{
    public string GroupKey { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
}
