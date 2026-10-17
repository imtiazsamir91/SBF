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

        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var model = new AdminDashboardViewModel();
            string connString = _configuration.GetConnectionString("OracleDb")!;

            using (var conn = new OracleConnection(connString))
            {
                await conn.OpenAsync();

               
                using (var cmd = new OracleCommand("SELECT (SELECT COUNT(*) FROM IMTIAZ_SAMIR.AppUsers WHERE RoleId = 3) as Users, (SELECT COUNT(*) FROM IMTIAZ_SAMIR.AppUsers WHERE RoleId = 2) as Sellers, (SELECT COUNT(*) FROM IMTIAZ_SAMIR.Categories) as Categories, (SELECT COUNT(*) FROM IMTIAZ_SAMIR.Bookings) as Bookings FROM DUAL", conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            model.TotalUsers = reader.GetInt32(0);
                            model.TotalSellers = reader.GetInt32(1);
                            model.TotalCategories = reader.GetInt32(2);
                            model.TotalBookings = reader.GetInt32(3);
                        }
                    }
                }

               
                string bookingQuery = @"SELECT b.BookingId, u.FullName, i.ItemName, b.TravelOrEventDate, b.Status, b.TotalAmount 
                                        FROM IMTIAZ_SAMIR.Bookings b
                                        JOIN IMTIAZ_SAMIR.AppUsers u ON b.UserId = u.UserId
                                        JOIN IMTIAZ_SAMIR.CategoryItems i ON b.ItemId = i.ItemId
                                        ORDER BY b.CreatedAt DESC";

                using (var cmd = new OracleCommand(bookingQuery, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            model.RecentBookings.Add(new AdminBookingDto
                            {
                                BookingId = reader.GetDecimal(0),
                                CustomerName = reader.GetString(1),
                                ItemName = reader.GetString(2),
                                TravelDate = reader.GetDateTime(3),
                                Status = reader.GetString(4),
                                TotalAmount = reader.GetDecimal(5)
                            });
                        }
                    }
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBookingStatus(decimal bookingId, string status)
        {
            string connString = _configuration.GetConnectionString("OracleDb")!;

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
            return RedirectToAction(nameof(Dashboard));
        }
    }
}