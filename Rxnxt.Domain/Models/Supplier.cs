using System.ComponentModel.DataAnnotations;

namespace Rxnxt.Domain.Models
{
    public class Supplier
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(30)]
        public string? Gstin { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    }
}
