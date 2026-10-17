using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EBS.Models;
using Oracle.ManagedDataAccess.Client;

namespace EBS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IConfiguration configuration, ILogger<AdminController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var model = new AdminDashboardViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    //  ( Admin=2, Seller=3, User=4)
                    string countQuery = @"SELECT 
                                            (SELECT COUNT(*) FROM IMTIAZ_SAMIR.AppUsers WHERE RoleId = 4) as Users, 
                                            (SELECT COUNT(*) FROM IMTIAZ_SAMIR.AppUsers WHERE RoleId = 3) as Sellers, 
                                            (SELECT COUNT(*) FROM IMTIAZ_SAMIR.Categories) as Categories, 
                                            (SELECT COUNT(*) FROM IMTIAZ_SAMIR.Bookings) as Bookings 
                                          FROM DUAL";

                    using (var cmd = new OracleCommand(countQuery, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                model.TotalUsers = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]);
                                model.TotalSellers = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]);
                                model.TotalCategories = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader[2]);
                                model.TotalBookings = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader[3]);
                            }
                        }
                    }

                    string bookingQuery = @"SELECT b.BookingId, u.FullName, i.ItemName, b.TravelOrEventDate, b.Status, b.TotalAmount 
                                            FROM IMTIAZ_SAMIR.Bookings b
                                            JOIN IMTIAZ_SAMIR.AppUsers u ON b.UserId = u.UserId
                                            JOIN IMTIAZ_SAMIR.CategoryItems i ON b.ItemId = i.ItemId
                                            ORDER BY b.BookingId DESC";

                    using (var cmd = new OracleCommand(bookingQuery, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.RecentBookings.Add(new AdminBookingDto
                                {
                                    BookingId = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0),
                                    CustomerName = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                                    ItemName = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                                    TravelDate = reader.IsDBNull(3) ? DateTime.Now : reader.GetDateTime(3),
                                    Status = reader.IsDBNull(4) ? "Pending" : reader.GetString(4),
                                    TotalAmount = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Admin Dashboard Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to load dashboard data: " + ex.Message;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBookingStatus(decimal bookingId, string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                TempData["ErrorMessage"] = "Status cannot be empty.";
                return RedirectToAction(nameof(Dashboard));
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    string query = "UPDATE IMTIAZ_SAMIR.Bookings SET Status = :p_Status WHERE BookingId = :p_BookingId";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        cmd.Parameters.Add("p_Status", OracleDbType.Varchar2).Value = status;
                        cmd.Parameters.Add("p_BookingId", OracleDbType.Decimal).Value = bookingId;

                        await conn.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = $"Booking #{bookingId} status updated to {status}.";
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Update Booking Status Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to update status: " + ex.Message;
            }

            return RedirectToAction(nameof(Dashboard));
        }
    }
}