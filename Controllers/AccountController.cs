using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace EBS.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IConfiguration configuration, ILogger<AccountController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError(string.Empty, "Email cannot be empty.");
                return View();
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;
            decimal userId = 0;
            decimal roleId = 0;
            string roleName = "User";

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // 🔑 ডাটাবেজ থেকে UserId, RoleId এবং RoleName তুলে আনা
                    string query = @"
                        SELECT u.UserId, u.RoleId, NVL(r.RoleName, 'User') AS RoleName
                        FROM IMTIAZ_SAMIR.AppUsers u
                        LEFT JOIN IMTIAZ_SAMIR.AppRoles r ON u.RoleId = r.RoleId
                        WHERE LOWER(TRIM(u.Email)) = LOWER(TRIM(:p_Email))";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        cmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = email.Trim();

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userId = reader.GetDecimal(0);
                                roleId = reader.IsDBNull(1) ? 4 : reader.GetDecimal(1);
                                roleName = reader.IsDBNull(2) ? "User" : reader.GetString(2);
                            }
                        }
                    }

                    // ইউজার ডাটাবেজে না থাকলে
                    if (userId == 0)
                    {
                        ModelState.AddModelError(string.Empty, "Invalid email or user not found.");
                        return View();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Database Error: {Message}", ex.Message);
                ModelState.AddModelError(string.Empty, "Database Error: " + ex.Message);
                return View();
            }

            // 🔑 সেশনে তথ্য সেভ করা
            string normalizedRole = roleName.Trim();
            HttpContext.Session.SetInt32("UserId", Convert.ToInt32(userId));
            HttpContext.Session.SetString("UserRole", normalizedRole.ToUpper()); // "ADMIN", "SELLER", "USER"

            // Cookie Claims সেটিং
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Role, normalizedRole),
                new Claim("UserId", userId.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            // 🎯 রোল অনুযায়ী নিখুঁত রিডাইরেক্ট লজিক
            if (roleId == 2 || normalizedRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else if (roleId == 3 || normalizedRole.Equals("Seller", StringComparison.OrdinalIgnoreCase))
            {
                // 🔑 Seller (RoleId = 3) সরাসরি সেলারের কাস্টমার রিকোয়েস্ট ফিডে পাঠাবে
                return RedirectToAction("CommunityFeed", "Seller");
            }
            else
            {
                // 🔑 User / Customer (RoleId = 4)
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("MyVenueRequests", "Customer");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}