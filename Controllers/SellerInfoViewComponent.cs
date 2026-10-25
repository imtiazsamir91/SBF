using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Threading.Tasks;

namespace EBS.ViewComponents
{
    public class SellerInfoViewComponent : ViewComponent
    {
        private readonly IConfiguration _configuration;

        public SellerInfoViewComponent(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // সেশন থেকে ইউজার আইডি নেওয়া (আপনার লগইন লজিক অনুযায়ী)
            int userId = HttpContext.Session.GetInt32("UserId") ?? 15;
            string sellerName = "Seller Panel";
            string itemName = "Venue Not Set";

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    string query = @"SELECT u.FullName, sc.ItemName 
                                     FROM IMTIAZ_SAMIR.AppUsers u
                                     LEFT JOIN IMTIAZ_SAMIR.SellerCategory sc ON u.UserId = sc.SellerId
                                     WHERE u.UserId = :p_UserId";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_UserId", OracleDbType.Decimal).Value = userId;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                sellerName = reader.IsDBNull(0) ? "Seller Panel" : reader.GetString(0);
                                itemName = reader.IsDBNull(1) ? "Venue Not Set" : reader.GetString(1);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                
            }

            
            ViewData["SellerName"] = sellerName;
            ViewData["ItemName"] = itemName;

            return View("_SellerInfoPartial");
        }
    }
}