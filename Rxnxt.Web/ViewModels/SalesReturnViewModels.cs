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
    public string BillNo { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public string CustomerID { get; set; } = string.Empty;
    public decimal? BillAmount { get; set; }
    public string? SaleId { get; set; }
}

public sealed class SalesReturnViewModel
{
    public SalesReturnFilterViewModel Filter { get; set; } = new();
    public List<SalesReturnRowViewModel> Rows { get; set; } = new();
}
