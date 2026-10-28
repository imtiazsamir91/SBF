using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Microsoft.AspNetCore.Http;

namespace EBS.Controllers
{
    public class BookingController : Controller
    {
        private readonly IConfiguration _configuration;

        public BookingController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBooking(decimal itemId, decimal seatId, DateTime travelOrEventDate, decimal totalAmount)
        {
            // ১. সেশন থেকে বর্তমান কাস্টমারের UserId নেওয়া
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (!sessionUserId.HasValue || sessionUserId.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please login first to book a seat or venue.";
                return RedirectToAction("Login", "Account");
            }

            int userId = sessionUserId.Value;
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // ২. আপনার দেওয়া আসল BOOKINGS টেবিলের কলাম অনুযায়ী ইনসার্ট কুয়েরি
                    string insertQuery = @"
                        INSERT INTO IMTIAZ_SAMIR.BOOKINGS 
                        (BOOKINGID, USERID, ITEMID, SEATID, TRAVELOREVENTDATE, STATUS, TOTALAMOUNT, CREATEDAT, LOCKEXPIRESAT)
                        VALUES 
                        (IMTIAZ_SAMIR.seq_bookings.NEXTVAL, :p_UserId, :p_ItemId, :p_SeatId, :p_EventDate, 'PENDING', :p_TotalAmount, SYSTIMESTAMP, SYSTIMESTAMP + INTERVAL '30' MINUTE)";

                    using (var cmd = new OracleCommand(insertQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_UserId", OracleDbType.Decimal).Value = userId;
                        cmd.Parameters.Add("p_ItemId", OracleDbType.Decimal).Value = itemId;
                        cmd.Parameters.Add("p_SeatId", OracleDbType.Decimal).Value = seatId;
                        cmd.Parameters.Add("p_EventDate", OracleDbType.Date).Value = travelOrEventDate;
                        cmd.Parameters.Add("p_TotalAmount", OracleDbType.Decimal).Value = totalAmount;

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Your seat booking request has been submitted successfully with PENDING status!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to process booking: " + ex.Message;
            }

            return RedirectToAction("Timeline", "Community");
        }
    }
}