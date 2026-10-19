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
            // লগইনকৃত সেলারের আইডি সেশন থেকে নেওয়া হচ্ছে (ডিফল্ট ১)
            return HttpContext.Session.GetInt32("UserId") ?? 1;
        }

        // ==========================================
        // 1. SELLER DASHBOARD
        // ==========================================
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

                    // 2. Call Updated Oracle Package Procedure (BookingEnginePkg)
                    using (var pkgCmd = new OracleCommand("IMTIAZ_SAMIR.BookingEnginePkg.get_seller_dashboard_stats", conn))
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

        // ==========================================
        // 2. SEAT CATEGORY MANAGEMENT & LIMIT CHECK
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ManageSeatCategories()
        {
            decimal sellerId = GetLoggedInSellerId();
            var model = new SeatCategoryManagementViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    string infoQuery = @"SELECT sc.SellerCatId, NVL(SUM(fs.SeatCapacity), 0) AS TotalCap
                                         FROM IMTIAZ_SAMIR.SellerCategory sc
                                         LEFT JOIN IMTIAZ_SAMIR.ItemFloors f ON sc.SellerCatId = f.SellerCatId
                                         LEFT JOIN IMTIAZ_SAMIR.FloorSections fs ON f.FloorId = fs.FloorId
                                         WHERE sc.SellerId = :p_SellerId
                                         GROUP BY sc.SellerCatId";

                    using (var cmd = new OracleCommand(infoQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                model.SellerCatId = reader.GetDecimal(0);
                                model.TotalCapacitySeats = Convert.ToInt32(reader.GetDecimal(1));
                            }
                        }
                    }

                    if (model.SellerCatId > 0)
                    {
                        string listQuery = @"SELECT SeatCatId, CategoryName, PricePerSeat, TotalSeats 
                                             FROM IMTIAZ_SAMIR.SeatCategories 
                                             WHERE SellerCatId = :p_ScId ORDER BY SeatCatId ASC";

                        using (var listCmd = new OracleCommand(listQuery, conn))
                        {
                            listCmd.BindByName = true;
                            listCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = model.SellerCatId;

                            using (var rdr = await listCmd.ExecuteReaderAsync())
                            {
                                while (await rdr.ReadAsync())
                                {
                                    int seats = Convert.ToInt32(rdr.GetDecimal(3));
                                    model.AllocatedSeatsCount += seats;

                                    model.ExistingCategories.Add(new SeatCategoryDto
                                    {
                                        SeatCatId = rdr.GetDecimal(0),
                                        CategoryName = rdr.GetString(1),
                                        PricePerSeat = rdr.GetDecimal(2),
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
                _logger.LogError("❌ Manage Seat Categories Error: {Message}", ex.Message);
            }

            model.RemainingSeats = model.TotalCapacitySeats - model.AllocatedSeatsCount;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSeatCategory(decimal sellerCatId, string categoryName, decimal pricePerSeat, int totalSeats)
        {
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new OracleCommand("IMTIAZ_SAMIR.BookingEnginePkg.allocate_seat_category", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.BindByName = true;

                        cmd.Parameters.Add("p_SellerCatId", OracleDbType.Decimal).Value = sellerCatId;
                        cmd.Parameters.Add("p_CategoryName", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(categoryName) ? "Regular" : categoryName.Trim();
                        cmd.Parameters.Add("p_PricePerSeat", OracleDbType.Decimal).Value = pricePerSeat;
                        cmd.Parameters.Add("p_AllocatedSeats", OracleDbType.Decimal).Value = totalSeats;

                        var statusParam = new OracleParameter("p_Status", OracleDbType.Varchar2, 50);
                        statusParam.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(statusParam);

                        await cmd.ExecuteNonQueryAsync();

                        string status = statusParam.Value.ToString() ?? "";

                        if (status == "EXCEEDED_LIMIT")
                        {
                            TempData["ErrorMessage"] = "Failed! Allocated seats exceed total capacity limit.";
                        }
                        else
                        {
                            TempData["SuccessMessage"] = "Seat category successfully allocated!";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Save Seat Category Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error: " + ex.Message;
            }

            return RedirectToAction(nameof(ManageSeatCategories));
        }

        // ==========================================
        // 3. CUSTOMER BOOKING REQUESTS (ACCEPT/REJECT)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> BookingRequests()
        {
            decimal sellerId = GetLoggedInSellerId();
            var model = new BookingRequestsViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    string query = @"SELECT DISTINCT b.BookingId, u.FullName, u.Email, u.PhoneNumber, 
                                            b.TravelOrEventDate, b.TotalAmount, b.Status, b.LockExpiresAt
                                     FROM IMTIAZ_SAMIR.Bookings b
                                     JOIN IMTIAZ_SAMIR.AppUsers u ON b.UserId = u.UserId
                                     JOIN IMTIAZ_SAMIR.SellerCategory sc ON b.ItemId = sc.SellerCatId
                                     WHERE sc.SellerId = :p_SellerId
                                     ORDER BY b.BookingId DESC";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.Requests.Add(new BookingRequestDto
                                {
                                    BookingId = reader.GetDecimal(0),
                                    CustomerName = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                                    CustomerEmail = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                                    CustomerPhone = reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                                    TravelOrEventDate = reader.GetDateTime(4),
                                    TotalAmount = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                                    Status = reader.GetString(6),
                                    LockExpiresAt = reader.IsDBNull(7) ? DateTime.Now : reader.GetDateTime(7)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Get Booking Requests Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error: " + ex.Message;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessRequest(decimal bookingId, string actionType, string? sellerNotes)
        {
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new OracleCommand("IMTIAZ_SAMIR.BookingEnginePkg.ProcessBookingRequest", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.BindByName = true;

                        cmd.Parameters.Add("p_BookingId", OracleDbType.Decimal).Value = bookingId;
                        cmd.Parameters.Add("p_Action", OracleDbType.Varchar2).Value = actionType.ToUpper();
                        cmd.Parameters.Add("p_SellerNotes", OracleDbType.Varchar2).Value = (object?)sellerNotes ?? DBNull.Value;

                        var statusParam = new OracleParameter("p_Status", OracleDbType.Varchar2, 50);
                        statusParam.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(statusParam);

                        await cmd.ExecuteNonQueryAsync();

                        string resultStatus = statusParam.Value.ToString() ?? "";

                        if (resultStatus == "ACCEPTED")
                        {
                            TempData["SuccessMessage"] = $"Booking #{bookingId} has been Accepted & Confirmed!";
                        }
                        else if (resultStatus == "REJECTED")
                        {
                            TempData["SuccessMessage"] = $"Booking #{bookingId} has been Rejected/Cancelled.";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Failed to process booking request.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Process Request Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error: " + ex.Message;
            }

            return RedirectToAction(nameof(BookingRequests));
        }
    }
}