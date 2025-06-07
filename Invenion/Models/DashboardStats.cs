// Models/DashboardStats.cs
namespace Invenion.Models
{
    public class DashboardStats
    {
        public int TotalEquipment { get; set; }
        public int AvailableEquipment { get; set; }
        public int BorrowedEquipment { get; set; }
        public int PendingRequests { get; set; }
        public int TotalStaff { get; set; }
        public int PendingRegistrations { get; set; }
    }
}
