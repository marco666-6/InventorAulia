using Microsoft.AspNetCore.Mvc;
using Invenion.Models;
using Invenion.Models.ViewModels;
using Invenion.Function;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using System.Net.Mail;
using System.Net;
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
                // validate dates
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

        // untuk change passoword 
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
        
        // Add these methods to your controller
        [HttpGet]
        public IActionResult ProcessOverdueRequests()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                var overdueUsers = new List<OverdueRequest>();

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_UpdateOverdueRequests", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                overdueUsers.Add(new OverdueRequest
                                {
                                    UserID = Convert.ToInt32(reader["UserID"]),
                                    RequestID = Convert.ToInt32(reader["RequestID"]),
                                    EquipmentID = Convert.ToInt32(reader["EquipmentID"]),
                                    EquipmentName = reader["EquipmentName"].ToString(),
                                    RequestedEndDate = Convert.ToDateTime(reader["RequestedEndDate"]),
                                    UserEmail = reader["UserEmail"].ToString(),
                                    UserFullName = reader["UserFullName"].ToString()
                                });
                            }
                        }
                    }
                }

                // Send emails to all overdue users
                int emailsSent = 0;
                foreach (var overdueRequest in overdueUsers)
                {
                    if (SendOverdueEmail(overdueRequest))
                    {
                        emailsSent++;
                    }
                }

                
                return Json(new { success = true, overdueCount = overdueUsers.Count, emailsSent = emailsSent });
            }
            catch (Exception ex)
            {
                
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ProcessOverdueReminders()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                var reminderUsers = new List<OverdueRequest>();

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetOverdueReminders", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                reminderUsers.Add(new OverdueRequest
                                {
                                    UserID = Convert.ToInt32(reader["UserID"]),
                                    RequestID = Convert.ToInt32(reader["RequestID"]),
                                    EquipmentID = Convert.ToInt32(reader["EquipmentID"]),
                                    EquipmentName = reader["EquipmentName"].ToString(),
                                    RequestedEndDate = Convert.ToDateTime(reader["RequestedEndDate"]),
                                    UserEmail = reader["UserEmail"].ToString(),
                                    UserFullName = reader["UserFullName"].ToString()
                                });
                            }
                        }
                    }
                }

                // Send reminder emails
                int emailsSent = 0;
                foreach (var reminderRequest in reminderUsers)
                {
                    if (SendReminderEmail(reminderRequest))
                    {
                        emailsSent++;
                    }
                }

                
                return Json(new { success = true, reminderCount = reminderUsers.Count, emailsSent = emailsSent });
            }
            catch (Exception ex)
            {
                
                return Json(new { success = false, error = ex.Message });
            }
        }

        private bool SendOverdueEmail(OverdueRequest overdueRequest)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("auliahusna104@gmail.com", "dpkh ogsb jusc pvyf"),
                    EnableSsl = true,
                };

                string body = $@"
                    <html>
                    <head>
                        <style>
                            body {{
                                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                                line-height: 1.6;
                                color: #333;
                                max-width: 600px;
                                margin: 0 auto;
                                padding: 20px;
                                background-color: #f9f9f9;
                            }}
                            .email-container {{
                                background-color: white;
                                border-radius: 8px;
                                padding: 30px;
                                box-shadow: 0 2px 10px rgba(0,0,0,0.1);
                            }}
                            .header {{
                                color: #e74c3c;
                                border-bottom: 2px solid #e74c3c;
                                padding-bottom: 10px;
                                margin-bottom: 20px;
                            }}
                            .logo {{
                                color: #e74c3c;
                                font-weight: bold;
                                font-size: 24px;
                            }}
                            .details {{
                                background-color: #fff5f5;
                                padding: 15px;
                                border-radius: 5px;
                                margin: 20px 0;
                                border-left: 4px solid #e74c3c;
                            }}
                            .detail-item {{
                                margin-bottom: 8px;
                            }}
                            .footer {{
                                margin-top: 30px;
                                font-size: 12px;
                                color: #7f8c8d;
                                text-align: center;
                            }}
                            .button {{
                                display: inline-block;
                                padding: 10px 20px;
                                background-color: #e74c3c;
                                color: white;
                                text-decoration: none;
                                border-radius: 5px;
                                margin: 20px 0;
                            }}
                            .highlight {{
                                font-weight: bold;
                                color: #e74c3c;
                            }}
                            .urgent {{
                                background-color: #ffebee;
                                border: 1px solid #e74c3c;
                                padding: 15px;
                                border-radius: 5px;
                                margin: 20px 0;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='email-container'>
                            <div class='header'>
                                <span class='logo'>⚠️ Invenion System</span>
                                <h1>URGENT: Equipment Return Overdue</h1>
                            </div>
                            
                            <div class='urgent'>
                                <h3>IMMEDIATE ACTION REQUIRED</h3>
                                <p>Your equipment borrowing request is now <span class='highlight'>OVERDUE</span>.</p>
                            </div>
                            
                            <p>Dear <strong>{overdueRequest.UserFullName}</strong>,</p>
                            
                            <p>This is an urgent notification that your borrowed equipment has exceeded its return deadline.</p>
                            
                            <div class='details'>
                                <h3>Overdue Equipment Details:</h3>
                                <div class='detail-item'><strong>Equipment:</strong> {overdueRequest.EquipmentName}</div>
                                <div class='detail-item'><strong>Request ID:</strong> {overdueRequest.RequestID}</div>
                                <div class='detail-item'><strong>Original Return Date:</strong> {overdueRequest.RequestedEndDate:yyyy-MM-dd}</div>
                                <div class='detail-item'><strong>Days Overdue:</strong> {(DateTime.Now - overdueRequest.RequestedEndDate).Days} days</div>
                            </div>
                            
                            <p><strong>Please return the equipment immediately to avoid further penalties.</strong></p>
                            
                            <p>If you have already returned the equipment, please contact the administrator immediately to update the system.</p>
                            
                            <a href='http://localhost:5070/' class='button'>Login to Your Account</a>
                        
                            <div class='footer'>
                                <p>Best regards,</p>
                                <p><strong>Invenion Admin Team</strong></p>
                                <p>© 2023 Invenion System. All rights reserved.</p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress("auliahusna104@gmail.com"),
                    Subject = "🚨 URGENT: Equipment Return Overdue - Action Required",
                    Body = body,
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(overdueRequest.UserEmail);

                smtpClient.Send(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending overdue email to {overdueRequest.UserEmail}: {ex.Message}");
                return false;
            }
        }

        private bool SendReminderEmail(OverdueRequest reminderRequest)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("auliahusna104@gmail.com", "dpkh ogsb jusc pvyf"),
                    EnableSsl = true,
                };

                string body = $@"
                    <html>
                    <head>
                        <style>
                            body {{
                                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                                line-height: 1.6;
                                color: #333;
                                max-width: 600px;
                                margin: 0 auto;
                                padding: 20px;
                                background-color: #f9f9f9;
                            }}
                            .email-container {{
                                background-color: white;
                                border-radius: 8px;
                                padding: 30px;
                                box-shadow: 0 2px 10px rgba(0,0,0,0.1);
                            }}
                            .header {{
                                color: #f39c12;
                                border-bottom: 2px solid #f39c12;
                                padding-bottom: 10px;
                                margin-bottom: 20px;
                            }}
                            .logo {{
                                color: #f39c12;
                                font-weight: bold;
                                font-size: 24px;
                            }}
                            .details {{
                                background-color: #fffaf0;
                                padding: 15px;
                                border-radius: 5px;
                                margin: 20px 0;
                                border-left: 4px solid #f39c12;
                            }}
                            .detail-item {{
                                margin-bottom: 8px;
                            }}
                            .footer {{
                                margin-top: 30px;
                                font-size: 12px;
                                color: #7f8c8d;
                                text-align: center;
                            }}
                            .button {{
                                display: inline-block;
                                padding: 10px 20px;
                                background-color: #f39c12;
                                color: white;
                                text-decoration: none;
                                border-radius: 5px;
                                margin: 20px 0;
                            }}
                            .highlight {{
                                font-weight: bold;
                                color: #f39c12;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='email-container'>
                            <div class='header'>
                                <span class='logo'>🔔 Invenion System</span>
                                <h1>Equipment Return Reminder</h1>
                            </div>
                            
                            <p>Dear <strong>{reminderRequest.UserFullName}</strong>,</p>
                            
                            <p>This is a friendly reminder that your borrowed equipment is due for return <span class='highlight'>tomorrow</span>.</p>
                            
                            <div class='details'>
                                <h3>Equipment Return Details:</h3>
                                <div class='detail-item'><strong>Equipment:</strong> {reminderRequest.EquipmentName}</div>
                                <div class='detail-item'><strong>Request ID:</strong> {reminderRequest.RequestID}</div>
                                <div class='detail-item'><strong>Return Date:</strong> {reminderRequest.RequestedEndDate:yyyy-MM-dd}</div>
                                <div class='detail-item'><strong>Time Remaining:</strong> Less than 24 hours</div>
                            </div>
                            
                            <p>Please ensure you return the equipment on time to avoid it being marked as overdue.</p>
                            
                            <p>If you need an extension or have any issues, please contact the administrator as soon as possible.</p>
                            
                            <a href='http://localhost:5070/' class='button'>Login to Your Account</a>
                        
                            <div class='footer'>
                                <p>Best regards,</p>
                                <p><strong>Invenion Admin Team</strong></p>
                                <p>© 2023 Invenion System. All rights reserved.</p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress("auliahusna104@gmail.com"),
                    Subject = "🔔 Reminder: Equipment Return Due Tomorrow",
                    Body = body,
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(reminderRequest.UserEmail);

                smtpClient.Send(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending reminder email to {reminderRequest.UserEmail}: {ex.Message}");
                return false;
            }
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