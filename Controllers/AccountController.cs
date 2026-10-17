using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
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
            string roleName = "User";

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    _logger.LogInformation("🎉 Database Connection Successful! Server Version: {Version}", conn.ServerVersion);

                    string query = @"SELECT u.UserId, r.RoleName 
                                     FROM IMTIAZ_SAMIR.AppUsers u
                                     JOIN IMTIAZ_SAMIR.AppRoles r ON u.RoleId = r.RoleId
                                     WHERE u.Email = :p_Email";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        cmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = email;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userId = reader.GetDecimal(0);
                                roleName = reader.GetString(1);
                            }
                        }
                    }

                    if (userId == 0)
                    {
                        if (email.ToLower().Contains("admin"))
                        {
                            roleName = "Admin";

                            decimal adminRoleId = 2;
                            using (var roleCmd = new OracleCommand("SELECT RoleId FROM IMTIAZ_SAMIR.AppRoles WHERE RoleName = 'Admin'", conn))
                            {
                                var roleRes = await roleCmd.ExecuteScalarAsync();
                                if (roleRes != null) adminRoleId = Convert.ToDecimal(roleRes);
                            }

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
                            userId = 4;
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

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Role, roleName),
                new Claim("UserId", userId.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (roleName == "Admin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}