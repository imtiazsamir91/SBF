using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Microsoft.AspNetCore.Http;
using EBS.Models;

namespace EBS.Controllers
{
    public class BookingController : Controller
    {
        private readonly IConfiguration _configuration;

        public BookingController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 1. GET: Dedicated Booking Page (Venue Details & Seat Selection)
        [HttpGet]
        public async Task<IActionResult> VenueDetails(decimal id)
        {
            string connString = _configuration.GetConnectionString("OracleDbContext")!;
            PostItem venue = null;

            using (var conn = new OracleConnection(connString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT sc.SellerCatId, sc.SellerId, u.FullName AS SellerName, sc.ItemName AS VenueTitle, 
                           sc.ServiceRating, u.Address AS Location
                    FROM IMTIAZ_SAMIR.SellerCategory sc
                    JOIN IMTIAZ_SAMIR.AppUsers u ON sc.SellerId = u.UserId
                    WHERE sc.SellerCatId = :p_SellerCatId";

                using (var cmd = new OracleCommand(query, conn))
                {
                    cmd.Parameters.Add("p_SellerCatId", OracleDbType.Decimal).Value = id;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            venue = new PostItem
                            {
                                PostId = reader.GetDecimal(0),
                                AuthorId = reader.GetDecimal(1),
                                AuthorName = reader.GetString(2),
                                Title = reader.GetString(3),
                                Description = reader.IsDBNull(4) ? "3 Star" : reader.GetString(4),
                                Location = reader.IsDBNull(5) ? "Dhaka" : reader.GetString(5),
                                Categories = new List<SellerCategoryDetail>()
                            };
                        }
                    }
                }

                if (venue == null) return NotFound();

                // Fetch Categories for pricing selection
                string catQuery = @"SELECT CategoryName, TotalSeats, PricePerSeat, SeatCatId 
                                    FROM IMTIAZ_SAMIR.SeatCategories 
                                    WHERE SellerCatId = :p_SellerCatId";
                using (var catCmd = new OracleCommand(catQuery, conn))
                {
                    catCmd.Parameters.Add("p_SellerCatId", OracleDbType.Decimal).Value = id;
                    using (var catReader = await catCmd.ExecuteReaderAsync())
                    {
                        while (await catReader.ReadAsync())
                        {
                            venue.Categories.Add(new SellerCategoryDetail
                            {
                                CategoryName = catReader.GetString(0),
                                TotalSeats = Convert.ToInt32(catReader.GetDecimal(1)),
                                PricePerSeat = catReader.GetDecimal(2)
                            });
                        }
                    }
                }
            }

            return View(venue);
        }

        // 2. POST: Submit Booking Request to Seller
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestBooking(decimal itemId, DateTime travelOrEventDate, decimal totalAmount)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (!sessionUserId.HasValue || sessionUserId.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please login first to send a booking request.";
                return RedirectToAction("Login", "Account");
            }

            int userId = sessionUserId.Value;
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            using (var conn = new OracleConnection(connString))
            {
                await conn.OpenAsync();

                // Check for available seat
                decimal seatId = 31; // Default seat logic or fetch first available SeatId

                string insertQuery = @"
                    INSERT INTO IMTIAZ_SAMIR.BOOKINGS 
                    (BOOKINGID, USERID, ITEMID, SEATID, TRAVELOREVENTDATE, STATUS, TOTALAMOUNT, CREATEDAT)
                    VALUES 
                    (IMTIAZ_SAMIR.seq_bookings.NEXTVAL, :p_UserId, :p_ItemId, :p_SeatId, :p_EventDate, 'PENDING', :p_TotalAmount, SYSTIMESTAMP)";

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

            TempData["SuccessMessage"] = "Booking request submitted successfully! Waiting for seller confirmation.";
            return RedirectToAction("MyBookings");
        }

        // 3. GET: Customer My Bookings Page (Shows Payment Option if Approved & Auto Cancel Handling)
        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            int userId = sessionUserId.HasValue ? sessionUserId.Value : 6;
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            var bookingsList = new List<dynamic>();

            using (var conn = new OracleConnection(connString))
            {
                await conn.OpenAsync();

                // 2 ঘণ্টার মধ্যে পেমেন্ট না করলে অটো-ক্যানসেল (AUTO-EXPIRE UNPAID APPROVED BOOKINGS)
                string autoCancelQuery = @"
                    UPDATE IMTIAZ_SAMIR.BOOKINGS 
                    SET STATUS = 'CANCELLED' 
                    WHERE STATUS = 'APPROVED' 
                    AND LOCKEXPIRESAT IS NOT NULL 
                    AND LOCKEXPIRESAT < SYSTIMESTAMP";
                using (var cancelCmd = new OracleCommand(autoCancelQuery, conn))
                {
                    await cancelCmd.ExecuteNonQueryAsync();
                }

                // Fetch customer's bookings
                string query = @"
                    SELECT b.BOOKINGID, sc.ItemName, b.TRAVELOREVENTDATE, b.TOTALAMOUNT, b.STATUS, b.LOCKEXPIRESAT
                    FROM IMTIAZ_SAMIR.BOOKINGS b
                    JOIN IMTIAZ_SAMIR.SellerCategory sc ON b.ITEMID = sc.SellerCatId
                    WHERE b.USERID = :p_UserId
                    ORDER BY b.BOOKINGID DESC";

                using (var cmd = new OracleCommand(query, conn))
                {
                    cmd.Parameters.Add("p_UserId", OracleDbType.Decimal).Value = userId;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            bookingsList.Add(new
                            {
                                BookingId = reader.GetDecimal(0),
                                VenueName = reader.GetString(1),
                                EventDate = reader.GetDateTime(2),
                                TotalAmount = reader.GetDecimal(3),
                                Status = reader.GetString(4),
                                LockExpiresAt = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5)
                            });
                        }
                    }
                }
            }

            return View(bookingsList);
        }
    }
}