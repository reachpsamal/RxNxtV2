using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Domain.Models
{
    public class PurchasePayment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PurchaseId { get; set; }

        [Required]
        [StringLength(20)]
        public string Method { get; set; } = "Cash";

        [StringLength(50)]
        public string? ReferenceNo { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [ForeignKey(nameof(PurchaseId))]
        public virtual Purchase? Purchase { get; set; }
    }
}
