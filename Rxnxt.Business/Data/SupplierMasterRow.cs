using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Business.Data
{
    public sealed class SupplierMasterRow
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        // [Key]
        [MaxLength(50)]
        public string UniqueID { get; set; } = string.Empty;

        [MaxLength(50)]
        public string SupplierCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string SupplierName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? MobileNumber { get; set; }

        public bool? ActiveStatus { get; set; }

        [MaxLength(50)]
        public string? CreatedBy { get; set; }

        public DateTime? CreatedDate { get; set; }

        public string? TenantId { get; set; }
    }
}
