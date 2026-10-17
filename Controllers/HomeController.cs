using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

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
            return View();
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
                using (var conn = new OracleConnection(connString))
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