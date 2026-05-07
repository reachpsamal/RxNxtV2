using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Domain.Models
{
    public class Purchase
    {
        [Key]
        public int Id { get; set; }

        public int? SupplierId { get; set; }

        [Required]
        [StringLength(100)]
        public string SupplierInvoiceNo { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime RefDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RoundOff { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrandTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAmount { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [ForeignKey(nameof(SupplierId))]
        public virtual Supplier? Supplier { get; set; }

        public virtual ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();

        public virtual ICollection<PurchasePayment> Payments { get; set; } = new List<PurchasePayment>();
    }
}
