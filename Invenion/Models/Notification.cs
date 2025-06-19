// Models/Notification.cs
namespace Invenion.Models
{
    public class Notification
    {
        public int NotificationID { get; set; }
        public int UserID { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public string? Type { get; set; } // Info, Warning, Success, Error
        public bool IsRead { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
//notifikasi yang dikirim ke user