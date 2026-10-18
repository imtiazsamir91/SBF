using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using EBS.Models;

namespace EBS.Controllers
{
    public class SellerController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SellerController> _logger;

        public SellerController(IConfiguration configuration, ILogger<SellerController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private decimal GetLoggedInSellerId()
        {
           
            return HttpContext.Session.GetInt32("UserId") ?? 1;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            decimal sellerId = GetLoggedInSellerId();
            var model = new SellerDashboardViewModel { SellerId = sellerId };
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // 1. Get Seller Info
                    string infoQuery = @"SELECT u.FullName, sc.ItemName, c.CategoryName, sc.SellerCatId 
                                         FROM IMTIAZ_SAMIR.AppUsers u
                                         JOIN IMTIAZ_SAMIR.SellerCategory sc ON u.UserId = sc.SellerId
                                         JOIN IMTIAZ_SAMIR.Categories c ON sc.CategoryId = c.CategoryId
                                         WHERE u.UserId = :p_SellerId";

                    decimal sellerCatId = 0;
                    using (var cmd = new OracleCommand(infoQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                model.SellerName = reader.GetString(0);
                                model.ItemName = reader.GetString(1);
                                model.CategoryName = reader.GetString(2);
                                sellerCatId = reader.GetDecimal(3);
                            }
                        }
                    }

                    // 2. Call Oracle Package Procedure for Stats
                    using (var pkgCmd = new OracleCommand("IMTIAZ_SAMIR.pkg_seller_management.get_seller_dashboard_stats", conn))
                    {
                        pkgCmd.CommandType = CommandType.StoredProcedure;
                        pkgCmd.BindByName = true;

                        pkgCmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;
                        pkgCmd.Parameters.Add("p_TotalFloors", OracleDbType.Decimal, ParameterDirection.Output);
                        pkgCmd.Parameters.Add("p_TotalSeats", OracleDbType.Decimal, ParameterDirection.Output);
                        pkgCmd.Parameters.Add("p_TotalCategories", OracleDbType.Decimal, ParameterDirection.Output);
                        pkgCmd.Parameters.Add("p_PendingRequests", OracleDbType.Decimal, ParameterDirection.Output);

                        await pkgCmd.ExecuteNonQueryAsync();

                        model.TotalFloors = Convert.ToInt32(((Oracle.ManagedDataAccess.Types.OracleDecimal)pkgCmd.Parameters["p_TotalFloors"].Value).Value);
                        model.TotalCapacitySeats = Convert.ToInt32(((Oracle.ManagedDataAccess.Types.OracleDecimal)pkgCmd.Parameters["p_TotalSeats"].Value).Value);
                        model.TotalCategoriesCount = Convert.ToInt32(((Oracle.ManagedDataAccess.Types.OracleDecimal)pkgCmd.Parameters["p_TotalCategories"].Value).Value);
                        model.PendingBookingRequests = Convert.ToInt32(((Oracle.ManagedDataAccess.Types.OracleDecimal)pkgCmd.Parameters["p_PendingRequests"].Value).Value);
                    }

                    // 3. Get Existing Seat Categories Breakdown
                    if (sellerCatId > 0)
                    {
                        string catListQuery = @"SELECT SeatCatId, CategoryName, PricePerSeat, TotalSeats 
                                                FROM IMTIAZ_SAMIR.SeatCategories 
                                                WHERE SellerCatId = :p_ScId ORDER BY SeatCatId ASC";

                        using (var catCmd = new OracleCommand(catListQuery, conn))
                        {
                            catCmd.BindByName = true;
                            catCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;

                            using (var catReader = await catCmd.ExecuteReaderAsync())
                            {
                                while (await catReader.ReadAsync())
                                {
                                    int seats = Convert.ToInt32(catReader.GetDecimal(3));
                                    model.AllocatedCategorySeats += seats;

                                    model.SeatCategories.Add(new SeatCategoryDto
                                    {
                                        SeatCatId = catReader.GetDecimal(0),
                                        CategoryName = catReader.GetString(1),
                                        PricePerSeat = catReader.GetDecimal(2),
                                        TotalSeats = seats
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Seller Dashboard Error: {Message}", ex.Message);
            }

            return View(model);
        }
    }
}