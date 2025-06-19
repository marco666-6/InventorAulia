// Models/BorrowingRequest.cs
using System.ComponentModel.DataAnnotations;

namespace Invenion.Models
{
    //references ini untuk tahu berapa kali & dimana class ini dipanggil
    public class BorrowingRequest
    {
        public int RequestID { get; set; }
        
        [Required] //tidak boleh kosong
        public int UserID { get; set; }
        
        //kalau tabel barang nya tidak diisi, bakal nampilin pesan error
        [Required(ErrorMessage = "Equipment selection is required")]
        public int EquipmentID { get; set; }
        
        public DateTime RequestDate { get; set; } = DateTime.Now;
        
        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)] //hanya tanggal, tidak menjelaskan waktu
        public DateTime RequestedStartDate { get; set; }
        
        [Required(ErrorMessage = "End date is required")]
        [DataType(DataType.Date)]
        public DateTime RequestedEndDate { get; set; }
        
        [Required(ErrorMessage = "Purpose is required")]
        [StringLength(500, ErrorMessage = "Purpose cannot exceed 500 characters")]
        public string? Purpose { get; set; }
        
        public string Status { get; set; } = "Pending";
        
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }
        
        [StringLength(500, ErrorMessage = "Rejection reason cannot exceed 500 characters")]
        public string? RejectionReason { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? ActualStartDate { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? ActualEndDate { get; set; }
        
        [StringLength(500, ErrorMessage = "Return condition cannot exceed 500 characters")]
        public string? ReturnCondition { get; set; }
        
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
        
        // Navigation properties
        //berelasi ke equipment
        public string? EquipmentCode { get; set; }
        public string? EquipmentName { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? RequesterName { get; set; }
        public string? Department { get; set; }
        public string? ApprovedByName { get; set; }
        public int? DaysOverdue { get; set; }
    }
}
