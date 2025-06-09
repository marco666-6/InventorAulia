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
        public IActionResult EditEquipment(Equipment model)
        {
            var authCheck = CheckAuth();
            if (authCheck != null) return authCheck;

            try
            {
                if (!ModelState.IsValid)
                {
                    LoadCategories();
                    return View(model);
                }

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_UpdateEquipment", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@EquipmentID", model.EquipmentID);
                        command.Parameters.AddWithValue("@EquipmentCode", model.EquipmentCode);
                        command.Parameters.AddWithValue("@EquipmentName", model.EquipmentName);
                        command.Parameters.AddWithValue("@CategoryID", model.CategoryID);
                        command.Parameters.AddWithValue("@Brand", model.Brand ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Model", model.Model ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@SerialNumber", model.SerialNumber ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Description", model.Description ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Status", model.Status);
                        command.Parameters.AddWithValue("@PurchaseDate", model.PurchaseDate ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@WarrantyExpiry", model.WarrantyExpiry ?? (object)DBNull.Value);

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
                return View(model);
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

                TempData["SuccessMessage"] = "User registration approved successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error approving registration. Please try again.";
            }

            return RedirectToAction("PendingRegistrations");
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
    }
}