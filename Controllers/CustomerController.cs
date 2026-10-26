using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using EBS.Models;

namespace EBS.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IConfiguration _configuration;

        public CustomerController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // GET: My Venue Requests Page
        public async Task<IActionResult> MyVenueRequests()
        {
            int? userIdNullable = HttpContext.Session.GetInt32("UserId");
            if (userIdNullable == null)
            {
                TempData["ErrorMessage"] = "Please log in to view your venue requests.";
                return RedirectToAction("Login", "Account");
            }
            int userId = userIdNullable.Value;

            var model = new CustomerVenueBookingViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // ১. Fetch Customer Requests (Safe Join with NVL and Column Aliases)
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT r.REQUEST_ID, 
                                   NVL(sc.CATEGORY_NAME, 'General Venue') AS CATEGORY_NAME, 
                                   NVL(r.TOTAL_SEATS, 0) AS TOTAL_SEATS, 
                                   NVL(r.TOTAL_PRICE, 0) AS TOTAL_PRICE, 
                                   NVL(r.STATUS, 'PENDING') AS STATUS, 
                                   NVL(r.REQUEST_DATE, SYSDATE) AS REQUEST_DATE, 
                                   r.NOTES
                            FROM IMTIAZ_SAMIR.VENUE_BOOKING_REQUESTS r
                            LEFT JOIN IMTIAZ_SAMIR.SELLER_CATEGORIES sc ON r.SELLER_CAT_ID = sc.SELLER_CAT_ID
                            WHERE r.USER_ID = :userId
                            ORDER BY r.REQUEST_DATE DESC";

                        cmd.Parameters.Add(new OracleParameter("userId", userId));

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.MyRequests.Add(new VenueRequestItem
                                {
                                    RequestId = reader.GetDecimal(0),
                                    VenueName = reader.GetString(1),
                                    TotalSeats = reader.GetInt32(2),
                                    TotalPrice = reader.GetDecimal(3),
                                    Status = reader.GetString(4),
                                    RequestDate = reader.GetDateTime(5),
                                    Notes = reader.IsDBNull(6) ? "" : reader.GetString(6)
                                });
                            }
                        }
                    }

                    // ২. Fetch Available Venues for Modal Dropdown
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT SELLER_CAT_ID, CATEGORY_NAME, TOTAL_CAPACITY FROM IMTIAZ_SAMIR.SELLER_CATEGORIES";
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.AvailableVenues.Add(new AvailableVenueItem
                                {
                                    SellerCatId = reader.GetDecimal(0),
                                    VenueName = reader.GetString(1),
                                    TotalCapacity = reader.GetInt32(2)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading venue requests: " + ex.Message;
            }

            return View(model);
        }

        // POST: Submit Venue Request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitVenueRequest(decimal sellerCatId, string notes)
        {
            int? userIdNullable = HttpContext.Session.GetInt32("UserId");
            if (userIdNullable == null)
            {
                return RedirectToAction("Login", "Account");
            }
            int userId = userIdNullable.Value;

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO IMTIAZ_SAMIR.VENUE_BOOKING_REQUESTS 
                            (REQUEST_ID, USER_ID, SELLER_CAT_ID, REQUEST_TYPE, TOTAL_SEATS, TOTAL_PRICE, STATUS, NOTES, REQUEST_DATE)
                            SELECT IMTIAZ_SAMIR.seq_venue_requests.NEXTVAL, :userId, sc.SELLER_CAT_ID, 'FULL_VENUE', sc.TOTAL_CAPACITY, NVL(sc.PRICE_PER_SEAT * sc.TOTAL_CAPACITY, 0), 'PENDING', :notes, SYSDATE
                            FROM IMTIAZ_SAMIR.SELLER_CATEGORIES sc WHERE sc.SELLER_CAT_ID = :sellerCatId";

                        cmd.Parameters.Add(new OracleParameter("userId", userId));
                        cmd.Parameters.Add(new OracleParameter("notes", notes ?? (object)DBNull.Value));
                        cmd.Parameters.Add(new OracleParameter("sellerCatId", sellerCatId));

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Venue reservation request submitted successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error submitting request: " + ex.Message;
            }

            return RedirectToAction(nameof(MyVenueRequests));
        }

        // POST: Cancel / Delete Request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelVenueRequest(decimal requestId)
        {
            int? userIdNullable = HttpContext.Session.GetInt32("UserId");
            if (userIdNullable == null)
            {
                return RedirectToAction("Login", "Account");
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "UPDATE IMTIAZ_SAMIR.VENUE_BOOKING_RESULTS SET STATUS = 'CANCELLED' WHERE REQUEST_ID = :requestId"; // টেবিল নেম রিকোয়ারমেন্ট অনুযায়ী চেক করে নিবেন
                        cmd.CommandText = "UPDATE IMTIAZ_SAMIR.VENUE_BOOKING_REQUESTS SET STATUS = 'CANCELLED' WHERE REQUEST_ID = :requestId";
                        cmd.Parameters.Add(new OracleParameter("requestId", requestId));
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Booking request has been cancelled successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error cancelling request: " + ex.Message;
            }

            return RedirectToAction(nameof(MyVenueRequests));
        }
    }
}