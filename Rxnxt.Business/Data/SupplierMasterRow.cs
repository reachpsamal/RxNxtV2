using System.ComponentModel.DataAnnotations;

namespace Rxnxt.Business.Data
{
    public sealed class SupplierMasterRow
    {
        public int ID { get; set; }

        [Key]
        [MaxLength(50)]
        public string UniqueID { get; set; } = string.Empty;

        [MaxLength(50)]
        public string SupplierCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string SupplierName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? MobileNumber { get; set; }
    }
}
