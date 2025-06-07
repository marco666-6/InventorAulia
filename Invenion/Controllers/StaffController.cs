using Microsoft.AspNetCore.Mvc;
using Invenion.Models;
using Invenion.Models.ViewModels;
using Invenion.Function;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Invenion.Controllers
{
    public class StaffController : Controller
    {
        private readonly DatabaseAccessLayer _dal;

        public StaffController()
        {
            _dal = new DatabaseAccessLayer();
        }

        // Check authentication and authorization
        private bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserID"));
        }

        private bool IsStaff()
        {
            var role = HttpContext.Session.GetString("Role");
            return role == "Staff" || role == "Admin"; // Admin can also access staff features
        }

        private IActionResult CheckAuth()
        {
            if (!IsAuthenticated())
                return RedirectToAction("Index", "Login");
            if (!IsStaff())
                return RedirectToAction("Index", "Login");
            return null;
        }
        
        private int GetUnreadNotificationCount()
        {
            if (!IsAuthenticated()) return 0;
            
            int userId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
            int count = 0;
            
            try
            {
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand(
                        "SELECT COUNT(*) FROM Notifications WHERE UserID = @UserID AND IsRead = 0", 
                        connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        connection.Open();
                        count = Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch
            {
                // If there's an error, just return 0
            }
            
            return count;
        }

        // Add this method to make unread count available to all views
        private void SetNotificationCount()
        {
            ViewBag.UnreadNotificationCount = GetUnreadNotificationCount();
        }

        // Also add an AJAX endpoint to get notification count
        [HttpGet]
        public IActionResult GetNotificationCount()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return Json(new { count = 0 });

            int count = GetUnreadNotificationCount();
            return Json(new { count = count });
        }

        // GET: Staff Dashboard
        public IActionResult Dashboard()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            SetNotificationCount(); // Add this line


            try
            {
                int userId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));

                // Get user's borrowing statistics
                var stats = new
                {
                    ActiveRequests = 0,
                    PendingRequests = 0,
                    CompletedRequests = 0,
                    RejectedRequests = 0
                };

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand(@"
                        SELECT 
                            COUNT(CASE WHEN Status IN ('Approved', 'Borrowed') THEN 1 END) as ActiveRequests,
                            COUNT(CASE WHEN Status = 'Pending' THEN 1 END) as PendingRequests,
                            COUNT(CASE WHEN Status = 'Returned' THEN 1 END) as CompletedRequests,
                            COUNT(CASE WHEN Status = 'Rejected' THEN 1 END) as RejectedRequests
                        FROM BorrowingRequests 
                        WHERE UserID = @UserID", connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                stats = new
                                {
                                    ActiveRequests = Convert.ToInt32(reader["ActiveRequests"]),
                                    PendingRequests = Convert.ToInt32(reader["PendingRequests"]),
                                    CompletedRequests = Convert.ToInt32(reader["CompletedRequests"]),
                                    RejectedRequests = Convert.ToInt32(reader["RejectedRequests"])
                                };
                            }
                        }
                    }
                }

                ViewBag.Stats = stats;
                return View();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading dashboard data.";
                return View();
            }
        }

        // GET: Available Equipment
        public IActionResult Equipment()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                List<Equipment> availableEquipment = new List<Equipment>();
                
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetAvailableEquipment", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        connection.Open();
                        
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                availableEquipment.Add(new Equipment
                                {
                                    EquipmentID = Convert.ToInt32(reader["EquipmentID"]),
                                    EquipmentCode = reader["EquipmentCode"].ToString(),
                                    EquipmentName = reader["EquipmentName"].ToString(),
                                    Brand = reader["Brand"]?.ToString(),
                                    Model = reader["Model"]?.ToString(),
                                    CategoryName = reader["CategoryName"].ToString()
                                });
                            }
                        }
                    }
                }

                return View(availableEquipment);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading equipment data.";
                return View(new List<Equipment>());
            }
        }

        // GET: Submit Borrowing Request
        public IActionResult SubmitRequest(int? equipmentId = null)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            var model = new BorrowingRequest();
            if (equipmentId.HasValue)
            {
                model.EquipmentID = equipmentId.Value;
            }

            LoadAvailableEquipment();
            return View(model);
        }

        // POST: Submit Borrowing Request (FR-05)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitRequest(BorrowingRequest model)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                // Validate dates
                if (model.RequestedStartDate < DateTime.Today)
                {
                    ModelState.AddModelError("RequestedStartDate", "Start date cannot be in the past.");
                }

                if (model.RequestedEndDate <= model.RequestedStartDate)
                {
                    ModelState.AddModelError("RequestedEndDate", "End date must be after start date.");
                }

                if (!ModelState.IsValid)
                {
                    LoadAvailableEquipment();
                    return View(model);
                }

                int userId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_SubmitBorrowingRequest", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@UserID", userId);
                        command.Parameters.AddWithValue("@EquipmentID", model.EquipmentID);
                        command.Parameters.AddWithValue("@RequestedStartDate", model.RequestedStartDate);
                        command.Parameters.AddWithValue("@RequestedEndDate", model.RequestedEndDate);
                        command.Parameters.AddWithValue("@Purpose", model.Purpose);

                        connection.Open();
                        var result = command.ExecuteScalar();

                        if (result != null)
                        {
                            TempData["SuccessMessage"] = "Borrowing request submitted successfully! Your request is pending approval.";
                            return RedirectToAction("MyRequests");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error submitting request. Please try again.");
            }

            LoadAvailableEquipment();
            return View(model);
        }

        // GET: My Borrowing Requests (FR-06)
        public IActionResult MyRequests()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                int userId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                List<BorrowingRequest> myRequests = new List<BorrowingRequest>();
                
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetUserBorrowingRequests", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@UserID", userId);
                        connection.Open();
                        
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                myRequests.Add(new BorrowingRequest
                                {
                                    RequestID = Convert.ToInt32(reader["RequestID"]),
                                    RequestDate = Convert.ToDateTime(reader["RequestDate"]),
                                    RequestedStartDate = Convert.ToDateTime(reader["RequestedStartDate"]),
                                    RequestedEndDate = Convert.ToDateTime(reader["RequestedEndDate"]),
                                    Purpose = reader["Purpose"].ToString(),
                                    Status = reader["Status"].ToString(),
                                    ApprovedDate = reader["ApprovedDate"] as DateTime?,
                                    RejectionReason = reader["RejectionReason"]?.ToString(),
                                    ActualStartDate = reader["ActualStartDate"] as DateTime?,
                                    ActualEndDate = reader["ActualEndDate"] as DateTime?,
                                    ReturnCondition = reader["ReturnCondition"]?.ToString(),
                                    EquipmentCode = reader["EquipmentCode"].ToString(),
                                    EquipmentName = reader["EquipmentName"].ToString(),
                                    Brand = reader["Brand"]?.ToString(),
                                    Model = reader["Model"]?.ToString(),
                                    ApprovedByName = reader["ApprovedByName"]?.ToString()
                                });
                            }
                        }
                    }
                }

                return View(myRequests);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading your requests.";
                return View(new List<BorrowingRequest>());
            }
        }

        // GET: Request Details
        public IActionResult RequestDetails(int id)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                int userId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                BorrowingRequest request = null;
                
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand(@"
                        SELECT br.*, e.EquipmentCode, e.EquipmentName, e.Brand, e.Model, 
                               approver.FullName as ApprovedByName
                        FROM BorrowingRequests br
                        INNER JOIN Equipment e ON br.EquipmentID = e.EquipmentID
                        LEFT JOIN Users approver ON br.ApprovedBy = approver.UserID
                        WHERE br.RequestID = @RequestID AND br.UserID = @UserID", connection))
                    {
                        command.Parameters.AddWithValue("@RequestID", id);
                        command.Parameters.AddWithValue("@UserID", userId);
                        connection.Open();
                        
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                request = new BorrowingRequest
                                {
                                    RequestID = Convert.ToInt32(reader["RequestID"]),
                                    RequestDate = Convert.ToDateTime(reader["RequestDate"]),
                                    RequestedStartDate = Convert.ToDateTime(reader["RequestedStartDate"]),
                                    RequestedEndDate = Convert.ToDateTime(reader["RequestedEndDate"]),
                                    Purpose = reader["Purpose"].ToString(),
                                    Status = reader["Status"].ToString(),
                                    ApprovedDate = reader["ApprovedDate"] as DateTime?,
                                    RejectionReason = reader["RejectionReason"]?.ToString(),
                                    ActualStartDate = reader["ActualStartDate"] as DateTime?,
                                    ActualEndDate = reader["ActualEndDate"] as DateTime?,
                                    ReturnCondition = reader["ReturnCondition"]?.ToString(),
                                    Notes = reader["Notes"]?.ToString(),
                                    EquipmentCode = reader["EquipmentCode"].ToString(),
                                    EquipmentName = reader["EquipmentName"].ToString(),
                                    Brand = reader["Brand"]?.ToString(),
                                    Model = reader["Model"]?.ToString(),
                                    ApprovedByName = reader["ApprovedByName"]?.ToString()
                                };
                            }
                        }
                    }
                }

                if (request == null)
                {
                    TempData["ErrorMessage"] = "Request not found.";
                    return RedirectToAction("MyRequests");
                }

                return View(request);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading request details.";
                return RedirectToAction("MyRequests");
            }
        }

        // GET: Change Password (FR-04)
        public IActionResult ChangePassword()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            return View();
        }

        // POST: Change Password (FR-04)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                int userId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                
                // Verify current password
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    // First, get current password hash
                    using (SqlCommand command = new SqlCommand("SELECT Password FROM Users WHERE UserID = @UserID", connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        connection.Open();
                        
                        string currentPasswordHash = command.ExecuteScalar()?.ToString();
                        string providedCurrentPasswordHash = SecurityHelper.HashPasswordMD5(model.CurrentPassword);
                        
                        if (!SecurityHelper.VerifyPassword(providedCurrentPasswordHash, currentPasswordHash))
                        {
                            ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                            return View(model);
                        }
                    }
                    
                    // Update password
                    using (SqlCommand command = new SqlCommand("sp_UpdateUserPassword", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@UserID", userId);
                        command.Parameters.AddWithValue("@NewPasswordHash", SecurityHelper.HashPasswordMD5(model.NewPassword));
                        
                        command.ExecuteNonQuery();
                        
                        TempData["SuccessMessage"] = "Password changed successfully!";
                        return RedirectToAction("Dashboard");
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error changing password. Please try again.");
                return View(model);
            }
        }

        // GET: User Profile
        public IActionResult Profile()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                int userId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                User user = null;
                
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetUserById", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@UserID", userId);
                        connection.Open();
                        
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                user = new User
                                {
                                    UserID = Convert.ToInt32(reader["UserID"]),
                                    Username = reader["Username"].ToString(),
                                    Email = reader["Email"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    Department = reader["Department"]?.ToString(),
                                    Role = reader["Role"].ToString(),
                                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                                };
                            }
                        }
                    }
                }

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User profile not found.";
                    return RedirectToAction("Dashboard");
                }

                return View(user);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading profile.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Notifications
        public IActionResult Notifications()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                int userId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                List<Notification> notifications = new List<Notification>();
                
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetUserNotifications", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@UserID", userId);
                        command.Parameters.AddWithValue("@Top", 50); // Get last 50 notifications
                        connection.Open();
                        
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                notifications.Add(new Notification
                                {
                                    NotificationID = Convert.ToInt32(reader["NotificationID"]),
                                    Title = reader["Title"].ToString(),
                                    Message = reader["Message"].ToString(),
                                    Type = reader["Type"].ToString(),
                                    IsRead = Convert.ToBoolean(reader["IsRead"]),
                                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                                });
                            }
                        }
                    }
                }

                return View(notifications);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading notifications.";
                return View(new List<Notification>());
            }
        }

        // POST: Mark Notification as Read
        [HttpPost]
        public IActionResult MarkNotificationRead(int notificationId)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return Json(new { success = false });

            try
            {
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_MarkNotificationAsRead", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@NotificationID", notificationId);
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false });
            }
        }

        // POST: Cancel Request (only for pending requests)
        [HttpPost]
        public IActionResult CancelRequest(int requestId)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return RedirectToAction("Index", "Login");

            try
            {
                int userId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand(@"
                        UPDATE BorrowingRequests 
                        SET Status = 'Cancelled', ModifiedDate = GETDATE()
                        WHERE RequestID = @RequestID AND UserID = @UserID AND Status = 'Pending'", connection))
                    {
                        command.Parameters.AddWithValue("@RequestID", requestId);
                        command.Parameters.AddWithValue("@UserID", userId);
                        connection.Open();
                        
                        int rowsAffected = command.ExecuteNonQuery();
                        
                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Request cancelled successfully.";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Unable to cancel request. Request may not be pending or doesn't belong to you.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error cancelling request.";
            }

            return RedirectToAction("MyRequests");
        }

        // Helper method to load available equipment for dropdown
        private void LoadAvailableEquipment()
        {
            try
            {
                List<Equipment> equipment = new List<Equipment>();
                
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetAvailableEquipment", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        connection.Open();
                        
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                equipment.Add(new Equipment
                                {
                                    EquipmentID = Convert.ToInt32(reader["EquipmentID"]),
                                    EquipmentCode = reader["EquipmentCode"].ToString(),
                                    EquipmentName = reader["EquipmentName"].ToString(),
                                    Brand = reader["Brand"]?.ToString(),
                                    Model = reader["Model"]?.ToString(),
                                    CategoryName = reader["CategoryName"].ToString()
                                });
                            }
                        }
                    }
                }

                ViewBag.AvailableEquipment = equipment;
            }
            catch (Exception ex)
            {
                ViewBag.AvailableEquipment = new List<Equipment>();
            }
        }

        // Logout (FR-13)
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Login");
        }
    }
}