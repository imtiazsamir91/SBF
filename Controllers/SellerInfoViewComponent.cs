using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

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
            // ১. সেশন থেকে সরাসরি কারেন্ট লগইন করা ইউজারের ID নেওয়া (কোনো হার্ডকোডেড আইডি ছাড়া)
            int? userIdNullable = HttpContext.Session.GetInt32("UserId");

            string sellerName = "Seller Panel";
            string itemName = "Venue Not Set";

            // সেশন না থাকলে বা ইউজার লগইন না থাকলে ডিফল্ট ভিউ রিটার্ন করবে
            if (userIdNullable == null)
            {
                ViewData["SellerName"] = sellerName;
                ViewData["ItemName"] = itemName;
                return View("_SellerInfoPartial");
            }

            int userId = userIdNullable.Value;
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // ২. লগইন করা নির্দিষ্ট ইউজারের ডাটাবেস রেকর্ড ডাইনামিকালি ফেচ করা
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
                // এরর লগিং বা হ্যান্ডলিং
            }

            ViewData["SellerName"] = sellerName;
            ViewData["ItemName"] = itemName;

            return View("_SellerInfoPartial");
        }
    }
}