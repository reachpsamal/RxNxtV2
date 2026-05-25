namespace Rxnxt.Web.ViewModels;

public sealed class DashboardViewModel
{
    public decimal TodaySaleAmount { get; set; }
    public int TodaySaleBills { get; set; }
    public int OpdSalesCount { get; set; }
    public int DirectSalesCount { get; set; }
    public int TodayPurchaseBills { get; set; }
    public decimal TodayPurchaseAmount { get; set; }
    public int TodayReturnBills { get; set; }
    public decimal TodayReturnAmount { get; set; }
    public int NearExpiryCount { get; set; }
    public List<TopMovingItem> Top20Items { get; set; } = new();
}

public sealed class TopMovingItem
{
    public int Rank { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Batch { get; set; } = string.Empty;
    public decimal QtySold { get; set; }
    public decimal Amount { get; set; }
}
