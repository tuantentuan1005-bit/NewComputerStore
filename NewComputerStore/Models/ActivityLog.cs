using System;
using System.ComponentModel.DataAnnotations;

namespace NewComputerStore.Models
{
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Action { get; set; }
        
        [Required]
        public string UserEmail { get; set; }
        
        public DateTime Time { get; set; } = DateTime.Now;
    }
}
