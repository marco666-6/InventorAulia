// Models/EquipmentCategory.cs
using System.ComponentModel.DataAnnotations;

namespace Invenion.Models
{
    public class EquipmentCategory
    {
        public int CategoryID { get; set; }
        
        [Required(ErrorMessage = "Category name is required")]
        [StringLength(50, ErrorMessage = "Category name cannot exceed 50 characters")]
        public string? CategoryName { get; set; }
        
        [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters")]
        public string? Description { get; set; }
        
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
