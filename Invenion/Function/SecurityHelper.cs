using System;
//untuk enkripsi dan kemananan
using System.Security.Cryptography;
using System.Text;

namespace Invenion.Function
{
    public static class SecurityHelper
    {
        /// <summary>
        /// Hashes a password using MD5 (Note: MD5 is considered insecure for password hashing in production,
        /// but using it here as requested)
        /// </summary>
        /// <param name="password">Plain text password</param>
        /// <returns>MD5 hash of the password</returns>
        public static string HashPasswordMD5(string password)
        {
            // Create MD5 hash from string
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(password);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Generates a random token for password reset
        /// </summary>
        /// <returns>Random token string</returns>
        public static string GenerateResetToken()
        {
            byte[] tokenBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            return Convert.ToBase64String(tokenBytes);
        }

        /// <summary>
        /// Verifies if the provided password hash matches the stored hash
        /// </summary>
        /// <param name="providedPasswordHash">Hash of the provided password</param>
        /// <param name="storedPasswordHash">Stored password hash</param>
        /// <returns>True if passwords match, false otherwise</returns>
        public static bool VerifyPassword(string providedPasswordHash, string storedPasswordHash)
        {
            return string.Equals(providedPasswordHash, storedPasswordHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}