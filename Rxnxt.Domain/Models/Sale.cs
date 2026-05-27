using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Domain.Models
{
    public class Sale
    {
        [Key]
        public int Id { get; set; }

        public int? CustomerId { get; set; }

        [Required]
        [Display(Name = "Sale Date")]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Sub Total")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Item Discount")]
        public decimal ItemDiscount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Additional Discount")]
        public decimal AdditionalDiscount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Grand Total")]
        public decimal GrandTotal { get; set; }

        [StringLength(50)]
        [Display(Name = "Payment Status")]
        public string PaymentStatus { get; set; } = "Pending";

        [StringLength(50)]
        [Display(Name = "Invoice Number")]
        public string? InvoiceNumber { get; set; }

        [NotMapped]
        public string? UniqueId { get; set; }

        // Navigation
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
