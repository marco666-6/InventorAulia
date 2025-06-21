using System;

namespace Invenion.Models;

// Add this model class to your Models folder or at the top of your controller
public class OverdueRequest
{
    public int UserID { get; set; }
    public int RequestID { get; set; }
    public int EquipmentID { get; set; }
    public string EquipmentName { get; set; }
    public DateTime RequestedEndDate { get; set; }
    public string UserEmail { get; set; }
    public string UserFullName { get; set; }
}
