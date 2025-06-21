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
    public class AdminController : Controller
    {
        private readonly DatabaseAccessLayer _dal;

        public AdminController()
        {
            _dal = new DatabaseAccessLayer();
        }

        // Check authentication and authorization
        private bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserID"));
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("Role") == "Admin";
        }

        private IActionResult CheckAuth()
        {
            if (!IsAuthenticated())
                return RedirectToAction("Index", "Login");
            if (!IsAdmin())
                return RedirectToAction("Dashboard", "Staff");
            return null;
        }

        // GET: Admin Dashboard
        public IActionResult Dashboard()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                DashboardStats stats = new DashboardStats();

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetDashboardStats", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                stats.TotalEquipment = Convert.ToInt32(reader["TotalEquipment"]);
                                stats.AvailableEquipment = Convert.ToInt32(reader["AvailableEquipment"]);
                                stats.MaintenancedEquipment = Convert.ToInt32(reader["MaintenancedEquipment"]);
                                stats.BorrowedEquipment = Convert.ToInt32(reader["BorrowedEquipment"]);
                                stats.PendingRequests = Convert.ToInt32(reader["PendingRequests"]);
                                stats.TotalStaff = Convert.ToInt32(reader["TotalStaff"]);
                                stats.PendingRegistrations = Convert.ToInt32(reader["PendingRegistrations"]);
                            }
                        }
                    }
                }

                return View(stats);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading dashboard data.";
                return View(new DashboardStats());
            }
        }

        // GET: Equipment Management
        public IActionResult Equipment()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                List<Equipment> equipmentList = new List<Equipment>();

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetAllEquipment", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                equipmentList.Add(new Equipment
                                {
                                    EquipmentID = Convert.ToInt32(reader["EquipmentID"]),
                                    EquipmentCode = reader["EquipmentCode"].ToString(),
                                    EquipmentName = reader["EquipmentName"].ToString(),
                                    Brand = reader["Brand"]?.ToString(),
                                    Model = reader["Model"]?.ToString(),
                                    SerialNumber = reader["SerialNumber"]?.ToString(),
                                    Description = reader["Description"]?.ToString(),
                                    Status = reader["Status"].ToString(),
                                    PurchaseDate = reader["PurchaseDate"] as DateTime?,
                                    WarrantyExpiry = reader["WarrantyExpiry"] as DateTime?,
                                    CategoryName = reader["CategoryName"].ToString()
                                });
                            }
                        }
                    }
                }

                return View(equipmentList);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading equipment data.";
                return View(new List<Equipment>());
            }
        }

        public IActionResult DELETE_EQUIPMENT(int id)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            int rowsAffected0, rowsAffected1;

            try
            {
                // DELETE EQUIOMENT HAPUS EQUIPMENT YES
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("DELETE FROM BorrowingRequests WHERE EquipmentID = @id;", connection))
                    {
                        command.CommandType = CommandType.Text;
                        command.Parameters.AddWithValue("@id", id); // Add parameter to prevent SQL injection
                        connection.Open();

                        rowsAffected0 = command.ExecuteNonQuery(); // Execute the command
                        connection.Close();
                    }

                    using (SqlCommand command = new SqlCommand("DELETE FROM Equipment WHERE EquipmentID = @id;", connection))
                    {
                        command.CommandType = CommandType.Text;
                        command.Parameters.AddWithValue("@id", id); // Add parameter to prevent SQL injection
                        connection.Open();

                        rowsAffected0 += command.ExecuteNonQuery(); // Execute the command
                        connection.Close();
                    }

                    if (rowsAffected0 > 0)
                    {
                        return Json(new { success = true, message = "Equipment deleted successfully." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "No equipment found with the given ID." });
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting equipment: " + ex.Message; // Log the exception message
                return Json(new { success = false, message = "An error occurred while deleting the equipment." });
            }
        }

        // GET: Add Equipment
        public IActionResult AddEquipment()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            LoadCategories();
            return View();
        }

        // POST: Add Equipment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddEquipment(Equipment Item)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                if (!ModelState.IsValid)
                {
                    LoadCategories();
                    return View(Item);
                }

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_AddEquipment", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@EquipmentCode", Item.EquipmentCode);
                        command.Parameters.AddWithValue("@EquipmentName", Item.EquipmentName);
                        command.Parameters.AddWithValue("@CategoryID", Item.CategoryID);
                        command.Parameters.AddWithValue("@Brand", Item.Brand ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Model", Item.Model ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@SerialNumber", Item.SerialNumber ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Description", Item.Description ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@PurchaseDate", Item.PurchaseDate ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@WarrantyExpiry", Item.WarrantyExpiry ?? (object)DBNull.Value);

                        connection.Open();
                        var result = command.ExecuteScalar();

                        if (result != null)
                        {
                            Console.WriteLine("Adding SUCCESSFUL - redirecting back");
                            TempData["SuccessMessage"] = "Equipment added successfully!";
                            return RedirectToAction("Equipment");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"SQL EXCEPTION caught: {ex.Message}");
                Console.WriteLine($"SQL Error Number: {ex.Number}");
                Console.WriteLine($"SQL Severity: {ex.Class}");
                Console.WriteLine($"SQL State: {ex.State}");
                if (ex.Message.Contains("EquipmentCode"))
                {
                    ModelState.AddModelError("EquipmentCode", "Equipment code already exists.");
                }
                else
                {
                    ModelState.AddModelError("", "Error adding equipment. Please try again.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GENERAL EXCEPTION caught: {ex.Message}");
                Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                ModelState.AddModelError("", "An error occurred. Please try again.");
            }

            LoadCategories();
            return View(Item);
        }

        // GET: Edit Equipment
        public IActionResult EditEquipment(int id)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                Equipment equipment = null;

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT e.*, c.CategoryName FROM Equipment e 
                          INNER JOIN EquipmentCategories c ON e.CategoryID = c.CategoryID 
                          WHERE e.EquipmentID = @EquipmentID", connection))
                    {
                        command.Parameters.AddWithValue("@EquipmentID", id);
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                equipment = new Equipment
                                {
                                    EquipmentID = Convert.ToInt32(reader["EquipmentID"]),
                                    EquipmentCode = reader["EquipmentCode"].ToString(),
                                    EquipmentName = reader["EquipmentName"].ToString(),
                                    CategoryID = Convert.ToInt32(reader["CategoryID"]),
                                    Brand = reader["Brand"]?.ToString(),
                                    Model = reader["Model"]?.ToString(),
                                    SerialNumber = reader["SerialNumber"]?.ToString(),
                                    Description = reader["Description"]?.ToString(),
                                    Status = reader["Status"].ToString(),
                                    PurchaseDate = reader["PurchaseDate"] as DateTime?,
                                    WarrantyExpiry = reader["WarrantyExpiry"] as DateTime?
                                };
                            }
                        }
                    }
                }

                if (equipment == null)
                {
                    TempData["ErrorMessage"] = "Equipment not found.";
                    return RedirectToAction("Equipment");
                }

                LoadCategories();
                return View(equipment);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading equipment data.";
                return RedirectToAction("Equipment");
            }
        }

        // POST: Edit Equipment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditEquipment(Equipment item)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                if (!ModelState.IsValid)
                {
                    LoadCategories();
                    return View(item);
                }

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_UpdateEquipment", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@EquipmentID", item.EquipmentID);
                        command.Parameters.AddWithValue("@EquipmentCode", item.EquipmentCode);
                        command.Parameters.AddWithValue("@EquipmentName", item.EquipmentName);
                        command.Parameters.AddWithValue("@CategoryID", item.CategoryID);
                        command.Parameters.AddWithValue("@Brand", item.Brand ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Model", item.Model ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@SerialNumber", item.SerialNumber ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Description", item.Description ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Status", item.Status);
                        command.Parameters.AddWithValue("@PurchaseDate", item.PurchaseDate ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@WarrantyExpiry", item.WarrantyExpiry ?? (object)DBNull.Value);

                        connection.Open();
                        command.ExecuteNonQuery();

                        TempData["SuccessMessage"] = "Equipment updated successfully!";
                        return RedirectToAction("Equipment");
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error updating equipment. Please try again.");
                LoadCategories();
                return View(item);
            }
        }

        // GET: Borrowing Requests Management
        public IActionResult BorrowingRequests()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                List<BorrowingRequest> requests = new List<BorrowingRequest>();

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetAllBorrowingRequests", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                requests.Add(new BorrowingRequest
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
                                    RequesterName = reader["RequesterName"].ToString(),
                                    Department = reader["Department"]?.ToString(),
                                    ApprovedByName = reader["ApprovedByName"]?.ToString()
                                });
                            }
                        }
                    }
                }

                return View(requests);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading borrowing requests.";
                return View(new List<BorrowingRequest>());
            }
        }

        // POST: Approve Borrowing Request
        [HttpPost]
        public IActionResult ApproveBorrowingRequest(int requestId, string notes = "")
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return RedirectToAction("Index", "Login");

            try
            {
                int adminUserId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_ApproveBorrowingRequest", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@RequestID", requestId);
                        command.Parameters.AddWithValue("@ApprovedBy", adminUserId);
                        command.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                TempData["SuccessMessage"] = "Borrowing request approved successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error approving request. Please try again.";
            }

            return RedirectToAction("BorrowingRequests");
        }

        // POST: Reject Borrowing Request
        [HttpPost]
        public IActionResult RejectBorrowingRequest(int requestId, string rejectionReason)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return RedirectToAction("Index", "Login");

            try
            {
                if (string.IsNullOrEmpty(rejectionReason))
                {
                    TempData["ErrorMessage"] = "Rejection reason is required.";
                    return RedirectToAction("BorrowingRequests");
                }

                int adminUserId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_RejectBorrowingRequest", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@RequestID", requestId);
                        command.Parameters.AddWithValue("@ApprovedBy", adminUserId);
                        command.Parameters.AddWithValue("@RejectionReason", rejectionReason);

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                TempData["SuccessMessage"] = "Borrowing request rejected successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error rejecting request. Please try again.";
            }

            return RedirectToAction("BorrowingRequests");
        }

        // POST: Return Equipment
        [HttpPost]
        public IActionResult ReturnEquipment(int requestId, string returnCondition, string notes = "")
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return RedirectToAction("Index", "Login");

            try
            {
                if (string.IsNullOrEmpty(returnCondition))
                {
                    TempData["ErrorMessage"] = "Return condition is required.";
                    return RedirectToAction("BorrowingRequests");
                }

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_ReturnEquipment", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@RequestID", requestId);
                        command.Parameters.AddWithValue("@ReturnCondition", returnCondition);
                        command.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                TempData["SuccessMessage"] = "Equipment returned successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error processing return. Please try again.";
            }

            return RedirectToAction("BorrowingRequests");
        }

        // GET: User Management
        public IActionResult Users()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                List<User> users = new List<User>();

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand(
                        "SELECT UserID, Username, Email, FullName, Department, Role, IsActive, IsApproved, CreatedDate FROM Users ORDER BY CreatedDate DESC",
                        connection))
                    {
                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new User
                                {
                                    UserID = Convert.ToInt32(reader["UserID"]),
                                    Username = reader["Username"].ToString(),
                                    Email = reader["Email"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    Department = reader["Department"]?.ToString(),
                                    Role = reader["Role"].ToString(),
                                    IsActive = Convert.ToBoolean(reader["IsActive"]),
                                    IsApproved = Convert.ToBoolean(reader["IsApproved"]),
                                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                                });
                            }
                        }
                    }
                }

                return View(users);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading users data.";
                return View(new List<User>());
            }
        }

        // GET: Pending Registrations
        public IActionResult PendingRegistrations()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                List<User> pendingUsers = new List<User>();

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetPendingRegistrations", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                pendingUsers.Add(new User
                                {
                                    UserID = Convert.ToInt32(reader["UserID"]),
                                    Username = reader["Username"].ToString(),
                                    Email = reader["Email"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    Department = reader["Department"]?.ToString(),
                                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                                });
                            }
                        }
                    }
                }

                return View(pendingUsers);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading pending registrations.";
                return View(new List<User>());
            }
        }

        // POST: Approve User Registration
        [HttpPost]
        public IActionResult ApproveRegistration(int userId)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return RedirectToAction("Index", "Login");

            try
            {
                int adminUserId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_ApproveUserRegistration", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@UserID", userId);
                        command.Parameters.AddWithValue("@ApprovedBy", adminUserId);

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                bool emailSent = SendWelcomeEmail(userId, adminUserId);

                TempData["SuccessMessage"] = "User registration approved successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error approving registration. Please try again.";
            }

            return RedirectToAction("PendingRegistrations");
        }

        // POST: Approve User Registration
        [HttpPost]
        public IActionResult DeactivateUser(int userId)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return RedirectToAction("Index", "Login");

            try
            {
                int adminUserId = Convert.ToInt32(HttpContext.Session.GetString("UserID"));

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("UPDATE Users SET IsActive = 0, ModifiedDate = GETDATE() WHERE UserID = @UserID; INSERT INTO Notifications (UserID, Title, Message, Type) VALUES (@UserID, 'Account Deactivated', 'Your account has been deactivated and is now inactive.', 'Success');", connection))
                    {
                        command.CommandType = CommandType.Text;
                        command.Parameters.AddWithValue("@UserID", userId);

                        connection.Open();
                        command.ExecuteNonQuery();
                        connection.Close();
                    }
                }

                bool emailSent = SendDeactivateEmail(userId, adminUserId);

                TempData["SuccessMessage"] = "User deactivated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deactivating account. Please try again.";
            }

            return RedirectToAction("Users");
        }

        [HttpPost]

        //untuk menghapus data user
        public IActionResult DeleteUser(int userId)
        {
            Console.WriteLine($"=== DELETE USER PROCESS STARTED ===");
            Console.WriteLine($"Target User ID: {userId}");
            Console.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            var authCheck = CheckAuth();
            if (authCheck != null) 
            {
                Console.WriteLine("ERROR: Authentication failed - redirecting to login");
                return RedirectToAction("Index", "Login");
            }
            Console.WriteLine("✓ Authentication passed");
            
            try
            {
                // Get admin user ID
                string adminUserIdStr = HttpContext.Session.GetString("UserID");
                Console.WriteLine($"Admin User ID from session: {adminUserIdStr}");
                
                if (string.IsNullOrEmpty(adminUserIdStr))
                {
                    Console.WriteLine("ERROR: No admin user ID found in session");
                    TempData["ErrorMessage"] = "Session expired. Please login again.";
                    return RedirectToAction("Index", "Login");
                }
                
                int adminUserId = Convert.ToInt32(adminUserIdStr);
                Console.WriteLine($"✓ Admin User ID converted: {adminUserId}");
                
                // STEP 1: Verify user exists before deletion
                Console.WriteLine("\n--- STEP 1: Verifying user exists ---");
                bool userExists = false;
                string userEmail = "";
                string userName = "";
                
                using (SqlConnection verifyConnection = new SqlConnection(_dal.GetConnectionString()))
                {
                    Console.WriteLine("Opening connection to verify user...");
                    verifyConnection.Open();
                    Console.WriteLine("✓ Connection opened for verification");
                    
                    using (SqlCommand verifyCommand = new SqlCommand("SELECT Username, Email FROM Users WHERE UserID = @UserID", verifyConnection))
                    {
                        verifyCommand.Parameters.AddWithValue("@UserID", userId);
                        Console.WriteLine($"Executing query: SELECT Username, Email FROM Users WHERE UserID = {userId}");
                        
                        using (var reader = verifyCommand.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userExists = true;
                                userName = reader["Username"]?.ToString() ?? "";
                                userEmail = reader["Email"]?.ToString() ?? "";
                                Console.WriteLine($"✓ User found - Username: {userName}, Email: {userEmail}");
                            }
                            else
                            {
                                Console.WriteLine($"ERROR: User with ID {userId} not found in database");
                            }
                        }
                    }
                    verifyConnection.Close();
                    Console.WriteLine("✓ Verification connection closed");
                }
                
                if (!userExists)
                {
                    Console.WriteLine("ABORT: User does not exist, cannot delete");
                    TempData["ErrorMessage"] = $"User with ID {userId} not found.";
                    return RedirectToAction("Users");
                }
                
                // STEP 2: Send deletion email first
                Console.WriteLine("\n--- STEP 2: Sending deletion email ---");
                bool emailSent = false;
                try
                {
                    emailSent = SendDeleteEmail(userId, adminUserId);
                    Console.WriteLine($"Email sending result: {(emailSent ? "SUCCESS" : "FAILED")}");
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"ERROR in email sending: {emailEx.Message}");
                    Console.WriteLine($"Email exception stack trace: {emailEx.StackTrace}");
                }
                
                // STEP 3: Update BorrowingRequests
                Console.WriteLine("\n--- STEP 3: Updating BorrowingRequests ---");
                try
                {
                    using (SqlConnection connection0 = new SqlConnection(_dal.GetConnectionString()))
                    {
                        Console.WriteLine("Opening connection for BorrowingRequests update...");
                        connection0.Open();
                        Console.WriteLine("✓ Connection opened for BorrowingRequests update");
                        
                        using (SqlCommand command0 = new SqlCommand("UPDATE BorrowingRequests SET ApprovedBy = NULL WHERE ApprovedBy = @UserID OR UserID = @UserID;", connection0))
                        {
                            command0.CommandType = CommandType.Text;
                            command0.Parameters.AddWithValue("@UserID", userId);
                            Console.WriteLine($"Executing: UPDATE BorrowingRequests SET ApprovedBy = 0 WHERE ApprovedBy = {userId} OR UserID = {userId}");
                            
                            int rowsAffected = command0.ExecuteNonQuery();
                            Console.WriteLine($"✓ BorrowingRequests update completed - {rowsAffected} rows affected");
                        }
                        connection0.Close();
                        Console.WriteLine("✓ BorrowingRequests connection closed");
                    }
                }
                catch (Exception step3Ex)
                {
                    Console.WriteLine($"ERROR in STEP 3 (BorrowingRequests update): {step3Ex.Message}");
                    Console.WriteLine($"STEP 3 exception stack trace: {step3Ex.StackTrace}");
                    throw; // Re-throw to be caught by main catch block
                }
                
                // STEP 4: Delete related records
                Console.WriteLine("\n--- STEP 4: Deleting related records ---");
                try
                {
                    using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                    {
                        Console.WriteLine("Opening connection for related records deletion...");
                        connection.Open();
                        Console.WriteLine("✓ Connection opened for related records deletion");
                        
                        using (SqlCommand command = new SqlCommand("DELETE FROM BorrowingRequests WHERE UserID = @UserID; DELETE FROM Notifications WHERE UserID = @UserID;", connection))
                        {
                            command.CommandType = CommandType.Text;
                            command.Parameters.AddWithValue("@UserID", userId);
                            Console.WriteLine($"Executing: DELETE FROM BorrowingRequests WHERE UserID = {userId}; DELETE FROM Notifications WHERE UserID = {userId}");
                            
                            int rowsAffected = command.ExecuteNonQuery();
                            Console.WriteLine($"✓ Related records deletion completed - {rowsAffected} rows affected");
                        }
                        connection.Close();
                        Console.WriteLine("✓ Related records connection closed");
                    }
                }
                catch (Exception step4Ex)
                {
                    Console.WriteLine($"ERROR in STEP 4 (Related records deletion): {step4Ex.Message}");
                    Console.WriteLine($"STEP 4 exception stack trace: {step4Ex.StackTrace}");
                    throw; // Re-throw to be caught by main catch block
                }
                
                // STEP 5: Delete user
                Console.WriteLine("\n--- STEP 5: Deleting user ---");
                try
                {
                    using (SqlConnection connection1 = new SqlConnection(_dal.GetConnectionString()))
                    {
                        Console.WriteLine("Opening connection for user deletion...");
                        connection1.Open();
                        Console.WriteLine("✓ Connection opened for user deletion");
                        
                        using (SqlCommand command1 = new SqlCommand("DELETE FROM Users WHERE UserID = @UserID;", connection1))
                        {
                            command1.CommandType = CommandType.Text;
                            command1.Parameters.AddWithValue("@UserID", userId);
                            Console.WriteLine($"Executing: DELETE FROM Users WHERE UserID = {userId}");
                            
                            int rowsAffected = command1.ExecuteNonQuery();
                            Console.WriteLine($"✓ User deletion completed - {rowsAffected} rows affected");
                            
                            if (rowsAffected == 0)
                            {
                                Console.WriteLine("WARNING: No user was deleted (0 rows affected)");
                            }
                        }
                        connection1.Close();
                        Console.WriteLine("✓ User deletion connection closed");
                    }
                }
                catch (Exception step5Ex)
                {
                    Console.WriteLine($"ERROR in STEP 5 (User deletion): {step5Ex.Message}");
                    Console.WriteLine($"STEP 5 exception stack trace: {step5Ex.StackTrace}");
                    throw; // Re-throw to be caught by main catch block
                }
                
                // SUCCESS
                Console.WriteLine("\n=== DELETE PROCESS COMPLETED SUCCESSFULLY ===");
                if (emailSent)
                {
                    Console.WriteLine("✓ User deleted successfully with email notification sent");
                    TempData["SuccessMessage"] = "User deleted successfully and notification email sent!";
                }
                else
                {
                    Console.WriteLine("✓ User deleted successfully but email notification failed");
                    TempData["SuccessMessage"] = "User deleted successfully, but email notification failed to send.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n=== CRITICAL ERROR IN DELETE PROCESS ===");
                Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"Error Message: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner Exception Stack Trace: {ex.InnerException.StackTrace}");
                }
                
                Console.WriteLine($"=== END ERROR DETAILS ===");
                
                TempData["ErrorMessage"] = $"Error deleting account: {ex.Message}";
            }
            
            Console.WriteLine($"Redirecting to Users action...");
            return RedirectToAction("Users");
        }

        // GET: Reports
        public IActionResult Reports()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            return View();
        }

        // GET: Borrowing Report
        public IActionResult BorrowingReport(DateTime? startDate, DateTime? endDate)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                List<BorrowingRequest> reportData = new List<BorrowingRequest>();

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetBorrowingReport", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        if (startDate.HasValue)
                            command.Parameters.AddWithValue("@StartDate", startDate.Value);
                        if (endDate.HasValue)
                            command.Parameters.AddWithValue("@EndDate", endDate.Value);

                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                reportData.Add(new BorrowingRequest
                                {
                                    RequestID = Convert.ToInt32(reader["RequestID"]),
                                    RequestDate = Convert.ToDateTime(reader["RequestDate"]),
                                    RequestedStartDate = Convert.ToDateTime(reader["RequestedStartDate"]),
                                    RequestedEndDate = Convert.ToDateTime(reader["RequestedEndDate"]),
                                    ActualStartDate = reader["ActualStartDate"] as DateTime?,
                                    ActualEndDate = reader["ActualEndDate"] as DateTime?,
                                    Status = reader["Status"].ToString(),
                                    Purpose = reader["Purpose"].ToString(),
                                    EquipmentCode = reader["EquipmentCode"].ToString(),
                                    EquipmentName = reader["EquipmentName"].ToString(),
                                    Brand = reader["Brand"]?.ToString(),
                                    Model = reader["Model"]?.ToString(),
                                    RequesterName = reader["RequesterName"].ToString(),
                                    Department = reader["Department"]?.ToString(),
                                    ApprovedByName = reader["ApprovedByName"]?.ToString(),
                                    DaysOverdue = reader["DaysOverdue"] as int?
                                });
                            }
                        }
                    }
                }

                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

                return View(reportData);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error generating report.";
                return View(new List<BorrowingRequest>());
            }
        }

        // Helper method to load categories for dropdown
        private void LoadCategories()
        {
            try
            {
                List<EquipmentCategory> categories = new List<EquipmentCategory>();

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetEquipmentCategories", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                categories.Add(new EquipmentCategory
                                {
                                    CategoryID = Convert.ToInt32(reader["CategoryID"]),
                                    CategoryName = reader["CategoryName"].ToString(),
                                    Description = reader["Description"]?.ToString()
                                });
                            }
                        }
                    }
                }

                ViewBag.Categories = categories;
            }
            catch (Exception ex)
            {
                ViewBag.Categories = new List<EquipmentCategory>();
            }
        }

        // GET: Get Categories for AJAX
        [HttpGet]
        public IActionResult GetCategories()
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                List<EquipmentCategory> categories = new List<EquipmentCategory>();

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_GetEquipmentCategories", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                categories.Add(new EquipmentCategory
                                {
                                    CategoryID = Convert.ToInt32(reader["CategoryID"]),
                                    CategoryName = reader["CategoryName"].ToString(),
                                    Description = reader["Description"]?.ToString(),
                                    IsActive = Convert.ToBoolean(reader["IsActive"]),
                                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                                });
                            }
                        }
                    }
                }

                return Json(new { success = true, data = categories });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading categories." });
            }
        }

        // POST: Add Category
        [HttpPost]
        public IActionResult AddCategory(string categoryName, string description)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                if (string.IsNullOrEmpty(categoryName))
                {
                    return Json(new { success = false, message = "Category name is required." });
                }

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_AddEquipmentCategory", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@CategoryName", categoryName);
                        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);

                        connection.Open();
                        var result = command.ExecuteScalar();

                        if (result != null)
                        {
                            return Json(new { success = true, message = "Category added successfully!", categoryId = Convert.ToInt32(result) });
                        }
                    }
                }

                return Json(new { success = false, message = "Failed to add category." });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("CategoryName"))
                {
                    return Json(new { success = false, message = "Category name already exists." });
                }
                return Json(new { success = false, message = "Error adding category." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred." });
            }
        }

        // POST: Update Category
        [HttpPost]
        public IActionResult UpdateCategory(int categoryId, string categoryName, string description)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                if (string.IsNullOrEmpty(categoryName))
                {
                    return Json(new { success = false, message = "Category name is required." });
                }

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_UpdateEquipmentCategory", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@CategoryID", categoryId);
                        command.Parameters.AddWithValue("@CategoryName", categoryName);
                        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);

                        connection.Open();
                        command.ExecuteNonQuery();

                        return Json(new { success = true, message = "Category updated successfully!" });
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("CategoryName"))
                {
                    return Json(new { success = false, message = "Category name already exists." });
                }
                return Json(new { success = false, message = "Error updating category." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred." });
            }
        }

        // POST: Delete Category
        [HttpPost]
        public IActionResult DeleteCategory(int categoryId)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_DeleteEquipmentCategory", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@CategoryID", categoryId);

                        connection.Open();
                        command.ExecuteNonQuery();

                        return Json(new { success = true, message = "Category deleted successfully!" });
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("REFERENCE"))
                {
                    return Json(new { success = false, message = "Cannot delete category as it is being used by equipment." });
                }
                return Json(new { success = false, message = "Error deleting category." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred." });
            }
        }

        private bool SendWelcomeEmail(int userId, int adminUserId)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("auliahusna104@gmail.com", "dpkh ogsb jusc pvyf"),
                    EnableSsl = true,
                };

                using (var connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    connection.Open();

                    // Get user details
                    string userQuery = "SELECT Username, Email, FullName, Department, Role, Password FROM Users WHERE UserID = @UserId";
                    var user = new { Username = "", Email = "", FullName = "", Department = "", Role = "", Password = "" };

                    using (var command = new SqlCommand(userQuery, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                user = new
                                {
                                    Username = reader["Username"].ToString(),
                                    Email = reader["Email"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    Department = reader["Department"].ToString(),
                                    Role = reader["Role"].ToString(),
                                    Password = reader["Password"].ToString()
                                };
                            }
                        }
                    }

                    // Get admin details
                    string adminQuery = "SELECT FullName FROM Users WHERE UserID = @AdminId";
                    string adminName = "";

                    using (var command = new SqlCommand(adminQuery, connection))
                    {
                        command.Parameters.AddWithValue("@AdminId", adminUserId);
                        var result = command.ExecuteScalar();
                        adminName = result?.ToString() ?? "Administrator";
                    }

                    // HTML email body
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
                                    color: #2c3e50;
                                    border-bottom: 2px solid #3498db;
                                    padding-bottom: 10px;
                                    margin-bottom: 20px;
                                }}
                                .logo {{
                                    color: #3498db;
                                    font-weight: bold;
                                    font-size: 24px;
                                }}
                                .details {{
                                    background-color: #f8f9fa;
                                    padding: 15px;
                                    border-radius: 5px;
                                    margin: 20px 0;
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
                                    background-color: #3498db;
                                    color: white;
                                    text-decoration: none;
                                    border-radius: 5px;
                                    margin: 20px 0;
                                }}
                                .highlight {{
                                    font-weight: bold;
                                    color: #e74c3c;
                                }}
                            </style>
                        </head>
                        <body>
                            <div class='email-container'>
                                <div class='header'>
                                    <span class='logo'>Invenion System</span>
                                    <h1>Welcome to Our Platform!</h1>
                                </div>
                                
                                <p>Dear <strong>{user.FullName}</strong>,</p>
                                
                                <p>We're excited to inform you that your account has been successfully created and approved by <strong>{adminName}</strong>.</p>
                                
                                <div class='details'>
                                    <div class='detail-item'><strong>Username:</strong> {user.Username}</div>
                                    <div class='detail-item'><strong>Email:</strong> {user.Email}</div>
                                    <div class='detail-item'><strong>Department:</strong> {user.Department}</div>
                                    <div class='detail-item'><strong>Role:</strong> {user.Role}</div>
                                </div>
                                
                                <p>For security reasons, please change your password immediately after logging in (optional).</p>
                                
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
                        Subject = "Welcome to Invenion System",
                        Body = body,
                        IsBodyHtml = true,
                    };
                    mailMessage.To.Add(user.Email);

                    smtpClient.Send(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error sending email: {ex.Message}");
                return false;
            }
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

        private bool SendDeactivateEmail(int userId, int adminUserId)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("auliahusna104@gmail.com", "dpkh ogsb jusc pvyf"),
                    EnableSsl = true,
                };

                using (var connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    connection.Open();

                    // Get user details
                    string userQuery = "SELECT Username, Email, FullName, Department, Role FROM Users WHERE UserID = @UserId";
                    var user = new { Username = "", Email = "", FullName = "", Department = "", Role = "" };

                    using (var command = new SqlCommand(userQuery, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                user = new
                                {
                                    Username = reader["Username"].ToString(),
                                    Email = reader["Email"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    Department = reader["Department"].ToString(),
                                    Role = reader["Role"].ToString()
                                };
                            }
                        }
                    }

                    // Get admin details
                    string adminQuery = "SELECT FullName FROM Users WHERE UserID = @AdminId";
                    string adminName = "";

                    using (var command = new SqlCommand(adminQuery, connection))
                    {
                        command.Parameters.AddWithValue("@AdminId", adminUserId);
                        var result = command.ExecuteScalar();
                        adminName = result?.ToString() ?? "Administrator";
                    }

                    // HTML email body
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
                                    color: #2c3e50;
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
                                    background-color: #f8f9fa;
                                    padding: 15px;
                                    border-radius: 5px;
                                    margin: 20px 0;
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
                                .warning {{
                                    background-color: #fff3cd;
                                    border: 1px solid #ffeaa7;
                                    padding: 15px;
                                    border-radius: 5px;
                                    margin: 20px 0;
                                    color: #856404;
                                }}
                                .highlight {{
                                    font-weight: bold;
                                    color: #e74c3c;
                                }}
                                .contact-info {{
                                    background-color: #e8f4f8;
                                    padding: 15px;
                                    border-radius: 5px;
                                    margin: 20px 0;
                                }}
                            </style>
                        </head>
                        <body>
                            <div class='email-container'>
                                <div class='header'>
                                    <span class='logo'>Invenion System</span>
                                    <h1>Account Deactivation Notice</h1>
                                </div>
                                
                                <p>Dear <strong>{user.FullName}</strong>,</p>
                                
                                <p>We are writing to inform you that your account has been <span class='highlight'>deactivated</span> by <strong>{adminName}</strong>.</p>
                                
                                <div class='details'>
                                    <div class='detail-item'><strong>Username:</strong> {user.Username}</div>
                                    <div class='detail-item'><strong>Email:</strong> {user.Email}</div>
                                    <div class='detail-item'><strong>Department:</strong> {user.Department}</div>
                                    <div class='detail-item'><strong>Role:</strong> {user.Role}</div>
                                    <div class='detail-item'><strong>Deactivation Date:</strong> {DateTime.Now:MMMM dd, yyyy}</div>
                                </div>
                                
                                <div class='warning'>
                                    <strong>Important:</strong> You will no longer be able to access the Invenion System with this account. All system privileges have been revoked.
                                </div>
                                
                                <p>If you believe this deactivation was made in error or if you have any questions regarding this action, please contact the administrator immediately.</p>
                                
                                <div class='contact-info'>
                                    <strong>Need Help?</strong><br>
                                    If you need to discuss this deactivation or require assistance, please contact your system administrator or IT support team.
                                </div>
                            
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
                        Subject = "Account Deactivation - Invenion System",
                        Body = body,
                        IsBodyHtml = true,
                    };
                    mailMessage.To.Add(user.Email);

                    smtpClient.Send(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error sending deactivation email: {ex.Message}");
                return false;
            }
        }

        private bool SendActivateEmail(int userId, int adminUserId)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("auliahusna104@gmail.com", "dpkh ogsb jusc pvyf"),
                    EnableSsl = true,
                };

                using (var connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    connection.Open();

                    // Get user details
                    string userQuery = "SELECT Username, Email, FullName, Department, Role FROM Users WHERE UserID = @UserId";
                    var user = new { Username = "", Email = "", FullName = "", Department = "", Role = "" };

                    using (var command = new SqlCommand(userQuery, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                user = new
                                {
                                    Username = reader["Username"].ToString(),
                                    Email = reader["Email"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    Department = reader["Department"].ToString(),
                                    Role = reader["Role"].ToString()
                                };
                            }
                        }
                    }

                    // Get admin details
                    string adminQuery = "SELECT FullName FROM Users WHERE UserID = @AdminId";
                    string adminName = "";

                    using (var command = new SqlCommand(adminQuery, connection))
                    {
                        command.Parameters.AddWithValue("@AdminId", adminUserId);
                        var result = command.ExecuteScalar();
                        adminName = result?.ToString() ?? "Administrator";
                    }

                    // HTML email body
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
                                    color: #2c3e50;
                                    border-bottom: 2px solid #27ae60;
                                    padding-bottom: 10px;
                                    margin-bottom: 20px;
                                }}
                                .logo {{
                                    color: #27ae60;
                                    font-weight: bold;
                                    font-size: 24px;
                                }}
                                .details {{
                                    background-color: #f8f9fa;
                                    padding: 15px;
                                    border-radius: 5px;
                                    margin: 20px 0;
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
                                    background-color: #27ae60;
                                    color: white;
                                    text-decoration: none;
                                    border-radius: 5px;
                                    margin: 20px 0;
                                }}
                                .success {{
                                    background-color: #d4edda;
                                    border: 1px solid #c3e6cb;
                                    padding: 15px;
                                    border-radius: 5px;
                                    margin: 20px 0;
                                    color: #155724;
                                }}
                                .highlight {{
                                    font-weight: bold;
                                    color: #27ae60;
                                }}
                                .welcome-back {{
                                    background-color: #e8f5e8;
                                    padding: 15px;
                                    border-radius: 5px;
                                    margin: 20px 0;
                                    border-left: 4px solid #27ae60;
                                }}
                            </style>
                        </head>
                        <body>
                            <div class='email-container'>
                                <div class='header'>
                                    <span class='logo'>Invenion System</span>
                                    <h1>Welcome Back! Account Activated Again</h1>
                                </div>
                                
                                <p>Dear <strong>{user.FullName}</strong>,</p>
                                
                                <p>We're pleased to inform you that your account has been <span class='highlight'>activated again</span> by <strong>{adminName}</strong>.</p>
                                
                                <div class='welcome-back'>
                                    <strong>Welcome back to the Invenion System!</strong><br>
                                    You can now access all system features again with your existing credentials.
                                </div>
                                
                                <div class='details'>
                                    <div class='detail-item'><strong>Username:</strong> {user.Username}</div>
                                    <div class='detail-item'><strong>Email:</strong> {user.Email}</div>
                                    <div class='detail-item'><strong>Department:</strong> {user.Department}</div>
                                    <div class='detail-item'><strong>Role:</strong> {user.Role}</div>
                                    <div class='detail-item'><strong>Reactivation Date:</strong> {DateTime.Now:MMMM dd, yyyy}</div>
                                </div>
                                
                                <div class='success'>
                                    <strong>Account Status:</strong> Your account is now active again and you have full access to the system. All your previous settings and data remain intact.
                                </div>
                                
                                <p>You can log into the system again using your existing username and password. If you need to reset your password, please contact the administrator.</p>
                                
                                <a href='http://localhost:5070/' class='button'>Login to Your Account Again</a>
                            
                                <div class='footer'>
                                    <p>We're glad to have you back!</p>
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
                        Subject = "Account Activated Again - Welcome Back to Invenion System",
                        Body = body,
                        IsBodyHtml = true,
                    };
                    mailMessage.To.Add(user.Email);

                    smtpClient.Send(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error sending activation email: {ex.Message}");
                return false;
            }
        }
        
        private bool SendDeleteEmail(int userId, int adminUserId)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("auliahusna104@gmail.com", "dpkh ogsb jusc pvyf"),
                    EnableSsl = true,
                };
                
                using (var connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    connection.Open();
                    
                    // Get user details
                    string userQuery = "SELECT Username, Email, FullName, Department, Role FROM Users WHERE UserID = @UserId";
                    var user = new { Username = "", Email = "", FullName = "", Department = "", Role = "" };
                    
                    using (var command = new SqlCommand(userQuery, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                user = new 
                                {
                                    Username = reader["Username"].ToString(),
                                    Email = reader["Email"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    Department = reader["Department"].ToString(),
                                    Role = reader["Role"].ToString()
                                };
                            }
                        }
                    }
                    
                    // Get admin details
                    string adminQuery = "SELECT FullName FROM Users WHERE UserID = @AdminId";
                    string adminName = "";
                    
                    using (var command = new SqlCommand(adminQuery, connection))
                    {
                        command.Parameters.AddWithValue("@AdminId", adminUserId);
                        var result = command.ExecuteScalar();
                        adminName = result?.ToString() ?? "Administrator";
                    }
                    
                    // HTML email body
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
                                    color: #2c3e50;
                                    border-bottom: 2px solid #c0392b;
                                    padding-bottom: 10px;
                                    margin-bottom: 20px;
                                }}
                                .logo {{
                                    color: #c0392b;
                                    font-weight: bold;
                                    font-size: 24px;
                                }}
                                .details {{
                                    background-color: #f8f9fa;
                                    padding: 15px;
                                    border-radius: 5px;
                                    margin: 20px 0;
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
                                .critical-warning {{
                                    background-color: #f8d7da;
                                    border: 1px solid #f5c6cb;
                                    padding: 20px;
                                    border-radius: 5px;
                                    margin: 20px 0;
                                    color: #721c24;
                                    border-left: 5px solid #c0392b;
                                }}
                                .highlight {{
                                    font-weight: bold;
                                    color: #c0392b;
                                }}
                                .data-loss-warning {{
                                    background-color: #fff3cd;
                                    border: 1px solid #ffeaa7;
                                    padding: 15px;
                                    border-radius: 5px;
                                    margin: 20px 0;
                                    color: #856404;
                                }}
                                .final-notice {{
                                    background-color: #343a40;
                                    color: white;
                                    padding: 20px;
                                    border-radius: 5px;
                                    margin: 20px 0;
                                    text-align: center;
                                }}
                            </style>
                        </head>
                        <body>
                            <div class='email-container'>
                                <div class='header'>
                                    <span class='logo'>Invenion System</span>
                                    <h1>Account Permanently Deleted</h1>
                                </div>
                                
                                <p>Dear <strong>{user.FullName}</strong>,</p>
                                
                                <p>This is to inform you that your account has been <span class='highlight'>permanently deleted</span> from the Invenion System by <strong>{adminName}</strong>.</p>
                                
                                <div class='details'>
                                    <div class='detail-item'><strong>Username:</strong> {user.Username}</div>
                                    <div class='detail-item'><strong>Email:</strong> {user.Email}</div>
                                    <div class='detail-item'><strong>Department:</strong> {user.Department}</div>
                                    <div class='detail-item'><strong>Role:</strong> {user.Role}</div>
                                    <div class='detail-item'><strong>Deletion Date:</strong> {DateTime.Now:MMMM dd, yyyy}</div>
                                </div>
                                
                                <div class='critical-warning'>
                                    <strong>⚠️ CRITICAL NOTICE:</strong><br>
                                    Your account and all associated access privileges have been permanently removed from the system. This action cannot be reversed.
                                </div>
                                
                                <div class='data-loss-warning'>
                                    <strong>Important Information:</strong><br>
                                    • All your personal data has been permanently removed<br>
                                    • You no longer have access to any system resources<br>
                                    • Any pending work or saved information has been deleted<br>
                                    • This email serves as your final notification
                                </div>
                                
                                <p>If you believe this deletion was made in error or if you have any urgent questions regarding this action, please contact the system administrator immediately. However, please note that account recovery may not be possible once deletion is complete.</p>
                                
                                <div class='final-notice'>
                                    <strong>FINAL NOTICE</strong><br>
                                    This is the last communication you will receive from the Invenion System regarding this account.
                                </div>
                            
                                <div class='footer'>
                                    <p>System Administration Team</p>
                                    <p><strong>Invenion System</strong></p>
                                    <p>© 2023 Invenion System. All rights reserved.</p>
                                </div>
                            </div>
                        </body>
                        </html>
                    ";

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress("auliahusna104@gmail.com"),
                        Subject = "Account Permanently Deleted - Invenion System",
                        Body = body,
                        IsBodyHtml = true,
                    };
                    mailMessage.To.Add(user.Email);
                    
                    smtpClient.Send(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error sending deletion email: {ex.Message}");
                return false;
            }
        }

    }
}