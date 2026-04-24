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
