using System.ComponentModel.DataAnnotations;

namespace Rxnxt.Domain.Models
{
    public class Medicine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(300)]
        [Display(Name = "Medicine Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        [Display(Name = "Generic Name")]
        public string? GenericName { get; set; }

        [StringLength(200)]
        public string? Manufacturer { get; set; }

        [StringLength(100)]
        public string? Category { get; set; }

        // Navigation
        public virtual ICollection<Batch> Batches { get; set; } = new List<Batch>();
    }
}
