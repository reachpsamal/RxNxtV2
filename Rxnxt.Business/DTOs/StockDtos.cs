using System;
using System.Collections.Generic;

namespace Rxnxt.Business.DTOs;

public sealed class StockDto
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
}

public sealed class StockResponseDto
{
    public List<StockDto>? Result { get; set; }
}
