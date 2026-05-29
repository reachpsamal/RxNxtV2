using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Business.Data;

[Table("StockMovement")]
public sealed class StockMovementRow
{
    [Key]
    public int ID { get; set; }

    [Required]
    [StringLength(50)]
    public string UniqueID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string ProductID { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ProductStockID { get; set; }

    [StringLength(50)]
    public string? BatchNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? MRP { get; set; }

    [StringLength(50)]
    public string? UnitID { get; set; }

    [StringLength(50)]
    public string? PackTypeID { get; set; }

    public string Direction { get; set; } = string.Empty;

    public string MovementType { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TransactionQty { get; set; }

    [StringLength(50)]
    public string? TransactionUOMID { get; set; }

    [StringLength(50)]
    public string? BaseUOMID { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? ConversionFactor { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal BaseQty { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal OpeningBalance { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExpectedClosingBalance { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? ClosingBalance { get; set; }

    [StringLength(50)]
    public string? ReferenceType { get; set; }

    [StringLength(50)]
    public string? ReferenceID { get; set; }

    [StringLength(50)]
    public string? ReferenceLineID { get; set; }

    [StringLength(100)]
    public string? ReferenceNo { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }

    [Required]
    [StringLength(50)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}
