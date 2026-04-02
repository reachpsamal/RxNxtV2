using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Domain.Models
{
    public class Batch
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MedicineId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Batch Number")]
        public string BatchNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Expiry Date")]
        [DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; }

        [Required]
        [Display(Name = "Strip Quantity")]
        public int StripQuantity { get; set; }

        [Required]
        [Display(Name = "Tablets Per Strip")]
        public int TabletPerStrip { get; set; } = 10;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Purchase Price")]
        public decimal PurchasePrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Selling Price (Strip)")]
        public decimal SellingPriceStrip { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Selling Price (Tablet)")]
        public decimal SellingPriceTablet { get; set; }

        // Navigation
        [ForeignKey("MedicineId")]
        public virtual Medicine? Medicine { get; set; }

        public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

        // Computed properties
        [NotMapped]
        public bool IsNearExpiry => ExpiryDate <= DateTime.Now.AddMonths(3);

        [NotMapped]
        public bool IsExpired => ExpiryDate <= DateTime.Now;

        [NotMapped]
        public int TotalTablets => StripQuantity * TabletPerStrip;
    }
}
