using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Domain.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SaleId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Payment Mode")]
        public string PaymentMode { get; set; } = "Cash"; // Cash, Card, UPI, Credit

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(100)]
        [Display(Name = "Reference")]
        public string? Reference { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Completed";

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("SaleId")]
        public virtual Sale? Sale { get; set; }
    }
}
