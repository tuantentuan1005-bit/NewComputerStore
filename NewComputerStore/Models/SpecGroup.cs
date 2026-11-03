using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewComputerStore.Models
{
    public class SpecGroup
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Tên nhóm")]
        public string Name { get; set; }

        [Required]
        [Display(Name = "Loại sản phẩm")]
        public int CategoryId { get; set; }

        [Display(Name = "Thứ tự")]
        public int DisplayOrder { get; set; } = 0;

        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        public virtual ICollection<SpecKey> SpecKeys { get; set; }

        public SpecGroup()
        {
            SpecKeys = new HashSet<SpecKey>();
        }
    }
}
