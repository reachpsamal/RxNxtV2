namespace Rxnxt.Business.DTOs;

public class CustomerSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int LoyaltyPoints { get; set; }
}

public class BatchSearchResult
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public int StripQuantity { get; set; }
    public int TabletPerStrip { get; set; }
    public decimal SellingPriceStrip { get; set; }
    public decimal SellingPriceTablet { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsNearExpiry { get; set; }
    public bool IsExpired { get; set; }
    public int TotalTablets { get; set; }
}

public class MedicineSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Manufacturer { get; set; }
    public string? Category { get; set; }
    public List<BatchSearchResult> Batches { get; set; } = new();
}

public sealed class StockSearchResult
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string TaxName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal AvailableQty { get; set; }
    public string UomName { get; set; } = string.Empty;
    public bool IsNearExpiry { get; set; }
    public bool IsExpired { get; set; }
}

public class CompleteSaleRequest
{
    public int? SaleId { get; set; }
    public bool ReturnMode { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public List<SaleItemRequest> Items { get; set; } = new();
    public decimal AdditionalDiscount { get; set; }
    public List<PaymentRequest> Payments { get; set; } = new();
}

public class SaleItemRequest
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public string UomName { get; set; } = string.Empty;
    public string? SaleUomName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string UnitType { get; set; } = "PCS";
    public decimal DiscountPercent { get; set; }
    public decimal TaxPercent { get; set; }
}

public class PaymentRequest
{
    public string PaymentMode { get; set; } = "Cash";
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
}

public class SaleResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? SaleId { get; set; }
    public string? InvoiceNumber { get; set; }
}
