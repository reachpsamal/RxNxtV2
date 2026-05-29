using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rxnxt.Business.Data;

[Table("SaleHeader")]
public sealed class SaleHeaderRow
{
    [Key]
    public int ID { get; set; }

    [Required]
    [StringLength(50)]
    public string UniqueID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string BillNo { get; set; } = string.Empty;

    public DateTime BillDate { get; set; }

    [Required]
    [StringLength(50)]
    public string BillType { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string CustomerID { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Narration { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BillAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ExtraAdd { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ExtraLess { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? RoundOff { get; set; }

    public bool ActiveStatus { get; set; }

    [Required]
    [StringLength(50)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    [StringLength(50)]
    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountPerc { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AmountBeforeTax { get; set; }

    [StringLength(50)]
    public string? StoreId { get; set; }
}

[Table("SaleDetail")]
public sealed class SaleDetailRow
{
    [Key]
    public int ID { get; set; }

    [Required]
    [StringLength(50)]
    public string UniqueID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string SaleID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string ProductID { get; set; } = string.Empty;

    [StringLength(50)]
    public string? BatchNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [StringLength(50)]
    public string? UnitID { get; set; }

    [StringLength(50)]
    public string? PackTypeID { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MRP { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? SalePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FreeQty { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Qty { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }

    [StringLength(50)]
    public string? BaseUOMID { get; set; }

    [StringLength(50)]
    public string? SaleUOMID { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SaleUOMQty { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ItemDiscPerc { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ItemDiscAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TaxableAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CGSTAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SGSTAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? IGSTAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalTaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ItemTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TaxPerc { get; set; }
}

[Table("SalePayment")]
public sealed class SalePaymentRow
{
    [Key]
    [StringLength(50)]
    public string PaymentId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string SaleId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string PaymentMode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [StringLength(100)]
    public string? ReferenceNo { get; set; }

    public DateTime PaymentDate { get; set; }
}

[Table("ProductMaster")]
public sealed class ProductMasterRow
{
    [Key]
    [StringLength(50)]
    public string UniqueID { get; set; } = string.Empty;

    [StringLength(300)]
    public string? ProductName { get; set; }

    [StringLength(50)]
    public string? UOMID { get; set; }

    [StringLength(50)]
    public string? OtherUOMID { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ConversionFactor { get; set; }
}

[Table("UOMMaster")]
public sealed class UomMasterRow
{
    [Key]
    public int ID { get; set; }

    [Required]
    [StringLength(50)]
    public string UniqueID { get; set; } = string.Empty;

    [StringLength(50)]
    public string? UOMName { get; set; }
}

[Table("CustomerMaster")]
public sealed class CustomerMasterRow
{
    [Key]
    public int ID { get; set; }

    [Required]
    [StringLength(50)]
    public string UniqueID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string CustomerCode { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string CustomerName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? MobileNumber { get; set; }

    public bool ActiveStatus { get; set; }

    [Required]
    [StringLength(50)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    [StringLength(50)]
    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }
}

[Table("SalesReturnHeader")]
public sealed class SalesReturnHeaderRow
{
    [Key]
    public int ID { get; set; }

    [Required]
    [StringLength(50)]
    public string UniqueID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string BillNo { get; set; } = string.Empty;

    public DateTime BillDate { get; set; }

    [Required]
    [StringLength(50)]
    public string BillType { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string CustomerID { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Narration { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BillAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ExtraAdd { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ExtraLess { get; set; }

    public bool ActiveStatus { get; set; }

    [Required]
    [StringLength(50)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    [StringLength(50)]
    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountPerc { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AmountBeforeTax { get; set; }

    [StringLength(50)]
    public string? SaleId { get; set; }

    [NotMapped]
    public decimal? RefundAmount { get; set; }

    [NotMapped]
    public decimal? CalculatedRefundAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? RoundOff { get; set; }
}

[Table("SalesReturnDetail")]
public sealed class SalesReturnDetailRow
{
    [Key]
    public int ID { get; set; }

    [Required]
    [StringLength(50)]
    public string UniqueID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string SaleID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string ProductID { get; set; } = string.Empty;

    [StringLength(50)]
    public string? BatchNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [StringLength(50)]
    public string? UnitID { get; set; }

    [StringLength(50)]
    public string? PackTypeID { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MRP { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? SalePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FreeQty { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Qty { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }

    [StringLength(50)]
    public string? BaseUOMID { get; set; }

    [StringLength(50)]
    public string? SaleUOMID { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SaleUOMQty { get; set; }
}

[Table("GRNHeader")]
public sealed class GrnHeaderRow
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ID { get; set; }

    [Key]
    [StringLength(50)]
    public string UniqueID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string GRNNo { get; set; } = string.Empty;

    public DateTime GRNDate { get; set; }

    [Required]
    [StringLength(50)]
    public string GRNType { get; set; } = "PURCHASE";

    [Required]
    [StringLength(50)]
    public string SupplierID { get; set; } = string.Empty;

    [StringLength(50)]
    public string? RefNumber { get; set; }

    public DateTime? RefDate { get; set; }

    [StringLength(50)]
    public string? POID { get; set; }

    [StringLength(50)]
    public string? WayBillNo { get; set; }

    public DateTime? WayBillDate { get; set; }

    [StringLength(500)]
    public string? Narration { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BillAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ExtraAdd { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ExtraLess { get; set; }

    public bool ActiveStatus { get; set; }

    [Required]
    [StringLength(50)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    [StringLength(50)]
    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalBeforeRoundOff { get; set; }
}

[Table("UserMaster")]
public sealed class UserMasterRow
{
    [Key]
    public int ID { get; set; }

    [StringLength(50)]
    public string? UserID { get; set; }

    [StringLength(100)]
    public string? UserName { get; set; }

    [StringLength(500)]
    public string? Password { get; set; }

    [StringLength(100)]
    public string? FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    [StringLength(20)]
    public string? MobileNumber { get; set; }

    [StringLength(50)]
    public string? UserGroup { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    public bool ActiveStatus { get; set; }

    [StringLength(50)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; }

    [StringLength(50)]
    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }
}

[Table("GRNDetail")]
public sealed class GrnDetailRow
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ID { get; set; }

    [Key]
    [StringLength(50)]
    public string UniqueID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string GRNID { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string ProductID { get; set; } = string.Empty;

    [StringLength(50)]
    public string? BatchNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [StringLength(50)]
    public string? UnitID { get; set; }

    [StringLength(50)]
    public string? PackTypeID { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MRP { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SalePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FreeQty { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Qty { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ItemDiscPerc { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ItemDiscAmount { get; set; }
}

[Table("ConfigSetting")]
public sealed class ConfigSettingRow
{
    [Key]
    public int ID { get; set; }

    [StringLength(50)]
    public string? UniqueID { get; set; }

    [StringLength(50)]
    public string? SettingGroup { get; set; }

    [StringLength(50)]
    public string? SettingKey { get; set; }

    [StringLength(500)]
    public string? SettingValue { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [StringLength(50)]
    public string? SettingLevel { get; set; }

    [StringLength(50)]
    public string? TenantId { get; set; }
}
