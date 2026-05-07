namespace Rxnxt.Web.ViewModels
{
    public sealed class PurchaseViewModel
    {
        public List<StockSearchItemViewModel> PrefetchedStocks { get; set; } = new();
    }
}
