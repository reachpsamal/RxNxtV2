using System;

namespace Rxnxt.Web.ViewModels;

public sealed class SalesHistoryFilterViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Query { get; set; }
}

public sealed class SalesHistoryRowViewModel
{
    public int Id { get; set; }
    public string? UniqueId { get; set; }
    public DateTime SaleDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}

public sealed class SalesHistoryViewModel
{
    public SalesHistoryFilterViewModel Filter { get; set; } = new();
    public List<SalesHistoryRowViewModel> Rows { get; set; } = new();
}
