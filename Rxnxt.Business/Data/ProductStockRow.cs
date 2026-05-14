using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Business.Data;

[Table("ProductStock")]
public sealed class ProductStockRow
{
    [Key]
    public int ID { get; set; }

    [Required]
    [StringLength(50)]
    public string ProductID { get; set; } = string.Empty;

    [StringLength(50)]
    public string? BatchNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PackQty { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }
}

[Table("VWGetProductStock")]
public sealed class ProductStockViewRow
{
    [StringLength(300)]
    public string ProductName { get; set; } = string.Empty;

    [StringLength(50)]
    public string ProductID { get; set; } = string.Empty;

    [StringLength(50)]
    public string ManufactureID { get; set; } = string.Empty;

    [StringLength(50)]
    public string? TaxID { get; set; }

    [StringLength(300)]
    public string ManufacturerName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? TaxName { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TaxPerc { get; set; }

    [StringLength(50)]
    public string UOMID { get; set; } = string.Empty;

    [StringLength(50)]
    public string? BatchNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MRP { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AvailableQty { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SalePrice { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }

    [StringLength(50)]
    public string UOMName { get; set; } = string.Empty;
}
