using Microsoft.AspNetCore.Mvc;
using Invenion.Models;
using Invenion.Models.ViewModels;
using Invenion.Function;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Net.Mail;
using System.Net;

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

        // Replace your existing ForgotPassword POST method with this complete implementation
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

                // First, check if user exists and get user details
                string userFullName = "";
                string userUsername = "";
                bool userExists = false;

                using (SqlConnection connection = new SqlConnection(_dal.GetConnectionString()))
                {
                    connection.Open();

                    // Check if user exists and get details
                    using (SqlCommand checkCommand = new SqlCommand(
                        "SELECT FullName, Username FROM Users WHERE Email = @Email AND IsActive = 1", 
                        connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Email", email);
                        using (SqlDataReader reader = checkCommand.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userExists = true;
                                userFullName = reader["FullName"].ToString();
                                userUsername = reader["Username"].ToString();
                            }
                        }
                    }

                    if (!userExists)
                    {
                        // For security reasons, don't reveal if email exists or not
                        TempData["SuccessMessage"] = "If your email is registered with us, you will receive password reset instructions shortly.";
                        return RedirectToAction("Index");
                    }

                    // Update user with reset token
                    using (SqlCommand updateCommand = new SqlCommand(
                        "UPDATE Users SET ResetToken = @ResetToken, ResetTokenExpiry = @Expiry WHERE Email = @Email AND IsActive = 1", 
                        connection))
                    {
                        updateCommand.Parameters.AddWithValue("@ResetToken", resetToken);
                        updateCommand.Parameters.AddWithValue("@Expiry", expiry);
                        updateCommand.Parameters.AddWithValue("@Email", email);

                        int rowsAffected = updateCommand.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Send password reset email
                            bool emailSent = SendPasswordResetEmail(email, userFullName, userUsername, resetToken);
                            
                            if (emailSent)
                            {
                                TempData["SuccessMessage"] = "Password reset instructions have been sent to your email.";
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Failed to send email. Please try again later.";
                            }
                            
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "An error occurred. Please try again.";
                            return View();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error (you might want to implement proper logging)
                Console.WriteLine($"Error in ForgotPassword: {ex.Message}");
                ModelState.AddModelError("", "An error occurred. Please try again.");
                return View();
            }
        }

        // Add this new method to send password reset emails
        private bool SendPasswordResetEmail(string email, string fullName, string username, string resetToken)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("auliahusna104@gmail.com", "dpkh ogsb jusc pvyf"),
                    EnableSsl = true,
                };

                // Create the reset URL (adjust the URL to match your application)
                string resetUrl = $"http://localhost:5070/Login/ResetPassword?token={resetToken}";

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
                            .warning {{
                                background-color: #fff3cd;
                                border: 1px solid #ffeaa7;
                                color: #856404;
                                padding: 15px;
                                border-radius: 5px;
                                margin: 20px 0;
                            }}
                            .button {{
                                display: inline-block;
                                padding: 12px 25px;
                                background-color: #e74c3c;
                                color: white;
                                text-decoration: none;
                                border-radius: 5px;
                                margin: 20px 0;
                                font-weight: bold;
                            }}
                            .footer {{
                                margin-top: 30px;
                                font-size: 12px;
                                color: #7f8c8d;
                                text-align: center;
                                border-top: 1px solid #ecf0f1;
                                padding-top: 20px;
                            }}
                            .security-note {{
                                font-size: 14px;
                                color: #e74c3c;
                                font-weight: bold;
                                margin: 15px 0;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='email-container'>
                            <div class='header'>
                                <span class='logo'>🔐 Invenion System</span>
                                <h1>Password Reset Request</h1>
                            </div>
                            
                            <p>Hello <strong>{fullName}</strong>,</p>
                            
                            <p>We received a request to reset the password for your account (<strong>{username}</strong>).</p>
                            
                            <div class='warning'>
                                <strong>⚠️ Security Notice:</strong> If you did not request this password reset, please ignore this email. Your account remains secure.
                            </div>
                            
                            <p>To reset your password, click the button below:</p>
                            
                            <a href='{resetUrl}' class='button'>Reset My Password</a>
                            
                            <p>Or copy and paste this link into your browser:</p>
                            <p style='word-break: break-all; background-color: #f8f9fa; padding: 10px; border-radius: 3px; font-family: monospace;'>{resetUrl}</p>
                            
                            <div class='security-note'>
                                🕐 This link will expire in 24 hours for security reasons.
                            </div>
                            
                            <p>If you're having trouble with the link above, you can also:</p>
                            <ul>
                                <li>Go to the login page</li>
                                <li>Click ""Forgot Password""</li>
                                <li>Enter your email address</li>
                            </ul>
                            
                            <div class='footer'>
                                <p>Best regards,</p>
                                <p><strong>Invenion Security Team</strong></p>
                                <p>© 2024 Invenion System. All rights reserved.</p>
                                <p style='margin-top: 10px;'>
                                    <small>This is an automated message. Please do not reply to this email.</small>
                                </p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress("auliahusna104@gmail.com", "Invenion System"),
                    Subject = "Password Reset Request - Invenion System",
                    Body = body,
                    IsBodyHtml = true,
                };
                
                mailMessage.To.Add(email);

                smtpClient.Send(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                // Log error (implement proper logging as needed)
                Console.WriteLine($"Error sending password reset email: {ex.Message}");
                return false;
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