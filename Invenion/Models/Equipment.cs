// Models/Equipment.cs
using System.ComponentModel.DataAnnotations;

namespace Invenion.Models
{
    public class Equipment
    {
        public int EquipmentID { get; set; }
        
        [Required(ErrorMessage = "Equipment code is required")]
        [StringLength(50, ErrorMessage = "Equipment code cannot exceed 50 characters")]
        public string? EquipmentCode { get; set; }
        
        [Required(ErrorMessage = "Equipment name is required")]
        [StringLength(100, ErrorMessage = "Equipment name cannot exceed 100 characters")]
        public string? EquipmentName { get; set; }
        
        [Required(ErrorMessage = "Category is required")]
        public int CategoryID { get; set; }
        
        [StringLength(50, ErrorMessage = "Brand cannot exceed 50 characters")]
        public string? Brand { get; set; }
        
        [StringLength(50, ErrorMessage = "Model cannot exceed 50 characters")]
        public string? Model { get; set; }
        
        [StringLength(100, ErrorMessage = "Serial number cannot exceed 100 characters")]
        public string? SerialNumber { get; set; }
        
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }
        
        public string Status { get; set; } = "Available";
        
        [DataType(DataType.Date)]
        public DateTime? PurchaseDate { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? WarrantyExpiry { get; set; }
        
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
        
        // Navigation properties
        public string? CategoryName { get; set; }
    }
}
