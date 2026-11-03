using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewComputerStore.Models
{
    public class SpecKey
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Tên thuộc tính")]
        public string Name { get; set; }

        [Required]
        [Display(Name = "Nhóm")]
        public int GroupId { get; set; }

        [Display(Name = "Thứ tự")]
        public int DisplayOrder { get; set; } = 0;

        [ForeignKey("GroupId")]
        public virtual SpecGroup Group { get; set; }

        public virtual ICollection<ProductSpec> ProductSpecs { get; set; }

        public SpecKey()
        {
            ProductSpecs = new HashSet<ProductSpec>();
        }
    }
}
