using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EBS.Models;
using Oracle.ManagedDataAccess.Client;

namespace EBS.Controllers
{
    [Authorize(Roles = "User")] // Only User role can request booking
    public class BookingController : Controller
    {
        private readonly IConfiguration _configuration;

        public BookingController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Create(decimal itemId)
        {
            var model = new BookingRequestViewModel
            {
                ItemId = itemId,
                TravelDate = DateTime.Today.AddDays(1)
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

           
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !decimal.TryParse(userIdClaim, out decimal userId))
            {
                // Fallback for testing / demo
                userId = 1;
            }

           
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            using (var conn = new OracleConnection(connString))
            {
                using (var cmd = new OracleCommand("IMTIAZ_SAMIR.BookingEnginePkg.LockSeatsForBooking", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // IN Parameters
                    cmd.Parameters.Add("p_UserId", OracleDbType.Decimal).Value = userId;
                    cmd.Parameters.Add("p_ItemId", OracleDbType.Decimal).Value = model.ItemId;
                    cmd.Parameters.Add("p_TravelDate", OracleDbType.Date).Value = model.TravelDate;
                    cmd.Parameters.Add("p_SeatIdList", OracleDbType.Varchar2).Value = model.SeatIdList ?? string.Empty;
                    cmd.Parameters.Add("p_SeatCount", OracleDbType.Int32).Value = model.SeatCount;
                    cmd.Parameters.Add("p_RequestFullVenue", OracleDbType.Int32).Value = model.RequestFullVenue;

                    // OUT Parameter for Booking ID
                    var pBookingId = new OracleParameter("p_BookingId", OracleDbType.Decimal, ParameterDirection.Output);
                    cmd.Parameters.Add(pBookingId);

                    try
                    {
                        await conn.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();

                        decimal bookingId = 0;
                        if (pBookingId.Value != DBNull.Value && pBookingId.Value != null)
                        {
                            bookingId = Convert.ToDecimal(pBookingId.Value.ToString());
                        }

                        TempData["SuccessMessage"] = $"Seats successfully locked for 10 minutes! Booking ID: {bookingId}";
                        return RedirectToAction("Payment", "Checkout", new { bookingId = bookingId });
                    }
                    catch (OracleException ex)
                    {
                        // PL/SQL Package Error Catch (RAISE_APPLICATION_ERROR)
                        ModelState.AddModelError(string.Empty, ex.Message);
                        return View(model);
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError(string.Empty, "Booking failed: " + ex.Message);
                        return View(model);
                    }
                }
            }
        }
    }
}