namespace Rxnxt.Web.ViewModels;

public sealed class SalesReturnFilterViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Query { get; set; }
}

public sealed class SalesReturnRowViewModel
{
    public int Id { get; set; }
    public string? UniqueId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public decimal GrandTotal { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}

public sealed class SalesReturnViewModel
{
    public SalesReturnFilterViewModel Filter { get; set; } = new();
    public List<SalesReturnRowViewModel> Rows { get; set; } = new();
}
