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
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, "Email and password cannot be empty.");
                return View();
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;
            decimal userId = 0;
            decimal roleId = 0;
            string dbPassword = string.Empty;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT UserId, RoleId, PasswordHash
                        FROM IMTIAZ_SAMIR.AppUsers
                        WHERE LOWER(TRIM(Email)) = LOWER(TRIM(:p_Email))";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        cmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = email.Trim();

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userId = reader.GetDecimal(0);
                                roleId = reader.IsDBNull(1) ? 4 : reader.GetDecimal(1);
                                dbPassword = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                            }
                        }
                    }

                    if (userId == 0)
                    {
                        ModelState.AddModelError(string.Empty, "Invalid email or user not found.");
                        return View();
                    }

                    // 🔑 একদম সহজ ও নিখুঁত পাসওয়ার্ড চেক (প্লেন টেক্সট অথবা ডিরেক্ট ম্যাচ)
                    // যদি ডেটাবেজের পাসওয়ার্ড এবং ফর্মের পাসওয়ার্ড হুবহু মিলে যায় অথবা ডিফল্ট পাসওয়ার্ড ম্যাচ করে
                    bool isPasswordValid = (dbPassword == password) || (password == "123456789");

                    if (!isPasswordValid)
                    {
                        ModelState.AddModelError(string.Empty, "Invalid password.");
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

            // রোল নির্ধারণ
            string roleName = "User";
            if (roleId == 2) roleName = "Admin";
            else if (roleId == 3) roleName = "Seller";
            else if (roleId == 4) roleName = "Customer";

            // সেশন সেটআপ
            HttpContext.Session.SetInt32("UserId", Convert.ToInt32(userId));
            HttpContext.Session.SetInt32("RoleId", Convert.ToInt32(roleId));
            HttpContext.Session.SetString("UserRole", roleName.ToUpper());

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Role, roleName),
                new Claim("UserId", userId.ToString()),
                new Claim("RoleId", roleId.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            // 🎯 রোল অনুযায়ী রিডাইরেক্ট
            if (roleId == 2)
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else if (roleId == 3)
            {
                // 🌟 সেলার লগইন করলে সরাসরি সেলার ড্যাশবোর্ডে চলে যাবে
                return RedirectToAction("Index", "Seller");
            }
            else
            {
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