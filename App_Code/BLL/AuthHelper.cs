using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Matome.Models;

namespace Matome.BLL
{
    public static class AuthHelper
    {
        private const string SessionUserKey = "CURRENT_USER";

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static void SignIn(User user)
        {
            if (HttpContext.Current != null && HttpContext.Current.Session != null)
            {
                HttpContext.Current.Session[SessionUserKey] = user;
            }
        }

        public static void SignOut()
        {
            if (HttpContext.Current != null && HttpContext.Current.Session != null)
            {
                HttpContext.Current.Session.Remove(SessionUserKey);
                HttpContext.Current.Session.Abandon();
            }
        }

        public static User GetCurrentUser()
        {
            if (HttpContext.Current != null && HttpContext.Current.Session != null)
            {
                return HttpContext.Current.Session[SessionUserKey] as User;
            }
            return null;
        }

        public static bool IsAuthenticated
        {
            get { return GetCurrentUser() != null; }
        }

        public static bool IsInRole(params string[] roles)
        {
            var user = GetCurrentUser();
            if (user == null) return false;
            foreach (var r in roles)
            {
                if (string.Equals(user.Role, r, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}

