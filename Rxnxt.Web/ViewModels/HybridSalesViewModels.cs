using System.Text.Json;
using System.Text.Encodings.Web;
using Rxnxt.Business.DTOs;

namespace Rxnxt.Web.ViewModels;

public sealed class StockSearchItemViewModel
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

    public static StockSearchItemViewModel FromDto(StockDto dto)
    {
        var today = DateTime.Today;
        var expiryDate = dto.ExpiryDate?.Date;
        var isExpired = expiryDate.HasValue && expiryDate.Value < today;
        var isNearExpiry = expiryDate.HasValue && !isExpired && expiryDate.Value <= today.AddDays(90);

        return new StockSearchItemViewModel
        {
            ProductId = dto.ProductId,
            ProductName = dto.ProductName,
            Manufacturer = dto.Manufacturer,
            TaxName = dto.TaxName,
            BatchNumber = dto.BatchNumber,
            ExpiryDate = dto.ExpiryDate,
            Mrp = dto.Mrp,
            AvailableQty = dto.AvailableQty,
            UomName = dto.UomName,
            IsExpired = isExpired,
            IsNearExpiry = isNearExpiry
        };
    }
}

public sealed class StockDetailsViewModel
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

    public static StockDetailsViewModel FromDto(StockDto dto)
    {
        return new StockDetailsViewModel
        {
            ProductId = dto.ProductId,
            ProductName = dto.ProductName,
            Manufacturer = dto.Manufacturer,
            TaxName = dto.TaxName,
            BatchNumber = dto.BatchNumber,
            ExpiryDate = dto.ExpiryDate,
            Mrp = dto.Mrp,
            AvailableQty = dto.AvailableQty,
            UomName = dto.UomName
        };
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}

public sealed class CustomerSearchItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CustomerCode { get; set; }

    public static CustomerSearchItemViewModel FromDto(CustomerSearchResult dto)
    {
        return new CustomerSearchItemViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            Phone = dto.Phone,
            Email = dto.Email,
            CustomerCode = dto.CustomerCode
        };
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}

public sealed class SaleSubmitViewModel
{
    public string SaleJson { get; set; } = string.Empty;
}
