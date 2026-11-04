using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EBS.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IActionResult Index()
        {
            // ১. সেশন থেকে ইউজার আইডি এবং রোল চেক করা
            int? userId = HttpContext.Session.GetInt32("UserId");
            string userRole = HttpContext.Session.GetString("UserRole") ?? "";

            // ২. যদি ইউজার লগইন করা থাকে, তবে রোল অনুযায়ী সঠিক ড্যাশবোর্ডে পাঠাবে
            if (userId != null && !string.IsNullOrEmpty(userRole))
            {
                // কাস্টমার হলে
                if (userRole.Equals("CUSTOMER", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("MyVenueRequests", "Customer");
                }
                // সেলার হলে
                else if (userRole.Equals("SELLER", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("CommunityFeed", "Seller");
                }
                // অ্যাডমিন হলে
                else if (userRole.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) || userRole.Equals("ADMINISTRATOR", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
            }

            // ৩. ইউজার লগইন করা না থাকলে সরাসরি লগইন পেজে রিডাইরেক্ট করবে
            return RedirectToAction("Login", "Account");
        }

        [HttpGet("test-db")]
        public async Task<IActionResult> TestDatabaseConnection()
        {
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            if (string.IsNullOrEmpty(connString))
            {
                return Content("❌ Error: 'OracleDbContext' connection string is missing or null!");
            }

            try
            {
                using (var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    string dbVersion = conn.ServerVersion;
                    return Content($"🎉 Database Connection Successful! Oracle Version: {dbVersion}");
                }
            }
            catch (Exception ex)
            {
                return Content($"❌ Database Connection Failed: {ex.Message}");
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}