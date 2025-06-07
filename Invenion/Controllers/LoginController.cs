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
    public class LoginController : Controller
    {
        private readonly DatabaseAccessLayer _dal;

        public LoginController()
        {
            _dal = new DatabaseAccessLayer();
        }

        // GET: Login
        public IActionResult Index()
        {
            // Redirect to dashboard if already logged in
            if (HttpContext.Session.GetString("UserID") != null)
            {
                var role = HttpContext.Session.GetString("Role");
                if (role == "Admin")
                    return RedirectToAction("Dashboard", "Admin");
                else
                    return RedirectToAction("Dashboard", "Staff");
            }
            return View();
        }

        // POST: Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(LoginViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Hash the password using MD5
                string hashedPassword = SecurityHelper.HashPasswordMD5(model.Password);

                // Authenticate user using stored procedure
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_AuthenticateUser", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@Username", model.Username);
                        command.Parameters.AddWithValue("@PasswordHash", hashedPassword);

                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Create session
                                HttpContext.Session.SetString("UserID", reader["UserID"].ToString());
                                HttpContext.Session.SetString("Username", reader["Username"].ToString());
                                HttpContext.Session.SetString("Email", reader["Email"].ToString());
                                HttpContext.Session.SetString("FullName", reader["FullName"].ToString());
                                HttpContext.Session.SetString("Department", reader["Department"]?.ToString() ?? "");
                                HttpContext.Session.SetString("Role", reader["Role"].ToString());

                                // Redirect based on role
                                string role = reader["Role"].ToString();
                                if (role == "Admin")
                                {
                                    return RedirectToAction("Dashboard", "Admin");
                                }
                                else
                                {
                                    return RedirectToAction("Dashboard", "Staff");
                                }
                            }
                            else
                            {
                                ModelState.AddModelError("", "Invalid username or password.");
                                return View(model);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred during login. Please try again.");
                return View(model);
            }
        }

        // GET: Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Hash the password
                string hashedPassword = SecurityHelper.HashPasswordMD5(model.Password);

                // Register user using stored procedure
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand("sp_RegisterUser", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@Username", model.Username);
                        command.Parameters.AddWithValue("@Email", model.Email);
                        command.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                        command.Parameters.AddWithValue("@FullName", model.FullName);
                        command.Parameters.AddWithValue("@Department", model.Department);

                        connection.Open();
                        var result = command.ExecuteScalar();

                        if (result != null)
                        {
                            Console.WriteLine("Registration SUCCESSFUL - redirecting to Index");
                            TempData["SuccessMessage"] = "Registration successful! Please wait for admin approval.";
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            Console.WriteLine("Registration FAILED - result is null");
                            ModelState.AddModelError("", "Registration failed. Please try again.");
                            return View(model);
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

                if (ex.Message.Contains("Username"))
                {
                    ModelState.AddModelError("Username", "Username already exists.");
                }
                else if (ex.Message.Contains("Email"))
                {
                    ModelState.AddModelError("Email", "Email already exists.");
                }
                else
                {
                    ModelState.AddModelError("", "Registration failed. Please try again.");
                }
                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GENERAL EXCEPTION caught: {ex.Message}");
                Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                ModelState.AddModelError("", "An error occurred during registration. Please try again.");
                return View(model);
            }
        }

        // GET: ForgotPassword
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    ModelState.AddModelError("", "Email is required.");
                    return View();
                }

                // Generate reset token
                string resetToken = SecurityHelper.GenerateResetToken();
                DateTime expiry = DateTime.Now.AddHours(24);

                // Update user with reset token
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand(
                        "UPDATE Users SET ResetToken = @ResetToken, ResetTokenExpiry = @Expiry WHERE Email = @Email AND IsActive = 1", 
                        connection))
                    {
                        command.Parameters.AddWithValue("@ResetToken", resetToken);
                        command.Parameters.AddWithValue("@Expiry", expiry);
                        command.Parameters.AddWithValue("@Email", email);

                        connection.Open();
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // In a real application, you would send an email here
                            TempData["SuccessMessage"] = "Password reset instructions have been sent to your email.";
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            ModelState.AddModelError("", "Email not found or account is inactive.");
                            return View();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred. Please try again.");
                return View();
            }
        }

        // GET: ResetPassword
        public IActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Index");
            }

            // Verify token exists and is not expired
            using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
            {
                using (SqlCommand command = new SqlCommand(
                    "SELECT UserID FROM Users WHERE ResetToken = @Token AND ResetTokenExpiry > GETDATE() AND IsActive = 1", 
                    connection))
                {
                    command.Parameters.AddWithValue("@Token", token);
                    connection.Open();
                    var result = command.ExecuteScalar();

                    if (result == null)
                    {
                        TempData["ErrorMessage"] = "Invalid or expired reset token.";
                        return RedirectToAction("Index");
                    }
                }
            }

            ViewBag.Token = token;
            return View();
        }

        // POST: ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(string token, string password, string confirmPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError("", "All fields are required.");
                    ViewBag.Token = token;
                    return View();
                }

                if (password != confirmPassword)
                {
                    ModelState.AddModelError("", "Passwords do not match.");
                    ViewBag.Token = token;
                    return View();
                }

                if (password.Length < 6)
                {
                    ModelState.AddModelError("", "Password must be at least 6 characters long.");
                    ViewBag.Token = token;
                    return View();
                }

                // Hash new password
                string hashedPassword = SecurityHelper.HashPasswordMD5(password);

                // Update password and clear reset token
                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    using (SqlCommand command = new SqlCommand(
                        "UPDATE Users SET Password = @Password, ResetToken = NULL, ResetTokenExpiry = NULL, ModifiedDate = GETDATE() WHERE ResetToken = @Token AND ResetTokenExpiry > GETDATE() AND IsActive = 1", 
                        connection))
                    {
                        command.Parameters.AddWithValue("@Password", hashedPassword);
                        command.Parameters.AddWithValue("@Token", token);

                        connection.Open();
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Password reset successful! You can now login with your new password.";
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            ModelState.AddModelError("", "Invalid or expired reset token.");
                            ViewBag.Token = token;
                            return View();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred. Please try again.");
                ViewBag.Token = token;
                return View();
            }
        }

        // Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["InfoMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Index");
        }

        // Check if user is authenticated
        private bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserID"));
        }

        // Get current user role
        private string GetCurrentUserRole()
        {
            return HttpContext.Session.GetString("Role") ?? "";
        }
    }
}