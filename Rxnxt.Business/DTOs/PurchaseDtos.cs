using System;
using System.Collections.Generic;

namespace Rxnxt.Business.DTOs
{
    public sealed class SupplierSearchResult
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Gstin { get; set; }
        public string? Address { get; set; }
    }

    public sealed class CompletePurchaseRequest
    {
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierInvoiceNo { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public DateTime RefDate { get; set; } = DateTime.Today;
        public DateTime? DueDate { get; set; }

        public decimal AdditionalDiscountAmount { get; set; }
        public decimal RoundOff { get; set; }

        public List<PurchaseItemRequest> Items { get; set; } = new();
        public List<PurchasePaymentRequest> Payments { get; set; } = new();
    }

    public sealed class PurchaseItemRequest
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public decimal Qty { get; set; }
        public decimal PurchaseRate { get; set; }
        public decimal Mrp { get; set; }

        public decimal DiscountPercent { get; set; }
        public decimal GstPercent { get; set; }
    }

    public sealed class PurchasePaymentRequest
    {
        public string Method { get; set; } = "Cash";
        public string? ReferenceNo { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class PurchaseResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? PurchaseId { get; set; }
        public string? SupplierInvoiceNo { get; set; }
    }
}
