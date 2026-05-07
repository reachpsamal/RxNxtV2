using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Domain.Models
{
    public class PurchaseItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PurchaseId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [StringLength(300)]
        public string ProductName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? BatchNumber { get; set; }

        public DateTime? ExpiryDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Qty { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchaseRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Mrp { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GstPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CgstPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SgstPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [ForeignKey(nameof(PurchaseId))]
        public virtual Purchase? Purchase { get; set; }
    }
}
