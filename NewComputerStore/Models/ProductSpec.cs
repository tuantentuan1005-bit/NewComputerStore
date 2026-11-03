using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewComputerStore.Models
{
    public class ProductSpec
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int SpecKeyId { get; set; }

        [StringLength(500)]
        public string Value { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [ForeignKey("SpecKeyId")]
        public virtual SpecKey SpecKey { get; set; }
    }
}
