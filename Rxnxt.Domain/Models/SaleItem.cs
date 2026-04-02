using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Domain.Models
{
    public class SaleItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SaleId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [StringLength(300)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string BatchNumber { get; set; } = string.Empty;

        public DateTime ExpiryDate { get; set; }

        [Required]
        [StringLength(20)]
        public string UomName { get; set; } = "PCS";

        [Required]
        public int Quantity { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Unit Type")]
        public string UnitType { get; set; } = "Strip"; // Strip or Tablet

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        [Display(Name = "Discount %")]
        public decimal DiscountPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Discount Amount")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        [Display(Name = "Tax %")]
        public decimal TaxPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Tax Amount")]
        public decimal TaxAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // Navigation
        [ForeignKey("SaleId")]
        public virtual Sale? Sale { get; set; }
    }
}
