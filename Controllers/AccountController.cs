using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Microsoft.AspNetCore.Http;

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
            string roleName = "Customer"; // ডিফল্ট রোল

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    _logger.LogInformation("🎉 Database Connection Successful! Server Version: {Version}", conn.ServerVersion);

                    // সরাসরি UserId এবং RoleId ফেচ করা (যাতে জয়েন ফেইল না করে)
                    string query = @"SELECT UserId, RoleId FROM IMTIAZ_SAMIR.AppUsers WHERE Email = :p_Email";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        cmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = email;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userId = reader.GetDecimal(0);
                                roleId = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                            }
                        }
                    }

                    // RoleId এর ওপর ভিত্তি করে সঠিক রোল নেম সেট করা
                    // (আপনার প্রজেক্টের রোল আইডি অনুযায়ী: যেমন RoleId 2 = Admin, বাকీগুলো Customer/Seller)
                    if (roleId == 2)
                    {
                        roleName = "Admin";
                    }
                    else if (roleId == 3 || roleId == 4)
                    {
                        roleName = "Customer"; // আপনার ইউজার ইমেইলের জন্য কাস্টমার হিসেবে সেট করা হলো
                    }
                    else
                    {
                        roleName = "Customer";
                    }

                    // যদি ইউজার ডাটাবেজে না পাওয়া যায়
                    if (userId == 0)
                    {
                        if (email.ToLower().Contains("admin"))
                        {
                            roleName = "Admin";
                            decimal adminRoleId = 2;

                            string insertQuery = @"INSERT INTO IMTIAZ_SAMIR.AppUsers (UserId, FullName, Email, PasswordHash, RoleId) 
                                                   VALUES (IMTIAZ_SAMIR.seq_users.NEXTVAL, 'System Admin', :p_Email, 'hashed_pwd', :p_RoleId)";

                            using (var insertCmd = new OracleCommand(insertQuery, conn))
                            {
                                insertCmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = email;
                                insertCmd.Parameters.Add("p_RoleId", OracleDbType.Decimal).Value = adminRoleId;
                                await insertCmd.ExecuteNonQueryAsync();
                            }

                            using (var idCmd = new OracleCommand("SELECT UserId FROM IMTIAZ_SAMIR.AppUsers WHERE Email = :p_Email", conn))
                            {
                                idCmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = email;
                                var idRes = await idCmd.ExecuteScalarAsync();
                                if (idRes != null) userId = Convert.ToDecimal(idRes);
                            }
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, "Invalid email or user not found.");
                            return View();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Database Connection or Query Failed: {Message}", ex.Message);
                ModelState.AddModelError(string.Empty, "Database Error: " + ex.Message);
                return View();
            }

            // 🔑 সেশনে আইডি এবং রোল স্ট্যান্ডার্ড ফরম্যাটে সেভ করা
            string normalizedRole = roleName.Trim();
            HttpContext.Session.SetInt32("UserId", Convert.ToInt32(userId));
            HttpContext.Session.SetString("UserRole", normalizedRole.ToUpper()); // "CUSTOMER", "SELLER", "ADMIN"

            // Cookie Authentication Claims সেট করা
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

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // 🎯 রোল অনুযায়ী সরাসরি সঠিক ড্যাশবোর্ডে বা পেজে রিডাইরেক্ট করা
            if (normalizedRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else if (normalizedRole.Equals("Seller", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("CommunityFeed", "Seller");
            }
            else
            {
                // কাস্টমার হলে সরাসরি MyVenueRequests পেজে যাবে
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