namespace Rxnxt.Web.ViewModels
{
    public class SaleViewModel
    {
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public List<SaleItemViewModel> Items { get; set; } = new();
        public decimal SubTotal { get; set; }
        public decimal ItemDiscount { get; set; }
        public decimal AdditionalDiscount { get; set; }
        public decimal GrandTotal { get; set; }
        public List<PaymentViewModel> Payments { get; set; } = new();
    }

    public class SaleItemViewModel
    {
        public int BatchId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string UnitType { get; set; } = "Strip";
        public decimal Price { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal Total { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public int AvailableQuantity { get; set; }
        public decimal SellingPriceStrip { get; set; }
        public decimal SellingPriceTablet { get; set; }
    }

    public class PaymentViewModel
    {
        public string PaymentMode { get; set; } = "Cash";
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }
}
