using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using EBS.Models;
using Microsoft.Extensions.Logging;

namespace EBS.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<CustomerController> _logger; // 🔑 লগার ফিল্ড যোগ করা হয়েছে

        public CustomerController(IConfiguration configuration, ILogger<CustomerController> logger)
        {
            _configuration = configuration;
            _logger = logger; // 🔑 কনস্ট্রাক্টরে লগার ইনিশিয়ালাইজ করা হয়েছে
        }

        // GET: My Venue Requests Page
        public async Task<IActionResult> MyVenueRequests(bool showExpired = false)
        {
            int? userIdNullable = HttpContext.Session.GetInt32("UserId");
            if (userIdNullable == null)
            {
                TempData["ErrorMessage"] = "Please log in to view your venue requests.";
                return RedirectToAction("Login", "Account");
            }
            int userId = userIdNullable.Value;

            ViewBag.ShowExpired = showExpired;
            var model = new CustomerVenueBookingViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // ১. Fetch Customer Requests
                    using (var cmd = conn.CreateCommand())
                    {
                        string dateCondition = showExpired
                            ? "AND r.REQUEST_DATE < TRUNC(SYSDATE)"
                            : "AND r.REQUEST_DATE >= TRUNC(SYSDATE)";

                        cmd.CommandText = $@"
                            SELECT r.REQUEST_ID, 
                                   NVL(sc.CATEGORYNAME, 'General Venue') AS CATEGORY_NAME, 
                                   NVL(r.TOTAL_SEATS, 0) AS TOTAL_SEATS, 
                                   NVL(r.TOTAL_PRICE, 0) AS TOTAL_PRICE, 
                                   NVL(r.STATUS, 'PENDING') AS STATUS, 
                                   NVL(r.REQUEST_DATE, SYSDATE) AS REQUEST_DATE, 
                                   r.NOTES
                            FROM IMTIAZ_SAMIR.VENUE_BOOKING_REQUESTS r
                            LEFT JOIN IMTIAZ_SAMIR.SEATCATEGORIES sc ON r.SELLER_CAT_ID = sc.SEATCATID
                            WHERE r.USER_ID = :userId
                            {dateCondition}
                            ORDER BY r.REQUEST_DATE DESC";

                        cmd.Parameters.Add(new OracleParameter("userId", userId));

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.MyRequests.Add(new VenueRequestItem
                                {
                                    RequestId = reader.GetDecimal(0),
                                    VenueName = reader.IsDBNull(1) ? "General Venue" : reader.GetString(1),
                                    TotalSeats = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetDecimal(2)),
                                    TotalPrice = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                                    Status = reader.IsDBNull(4) ? "PENDING" : reader.GetString(4),
                                    RequestDate = reader.IsDBNull(5) ? DateTime.Now : reader.GetDateTime(5),
                                    Notes = reader.IsDBNull(6) ? "" : reader.GetString(6)
                                });
                            }
                        }
                    }

                    // ২. Fetch Available Venues for Modal Dropdown
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT SEATCATID, CATEGORYNAME, TOTALSEATS FROM IMTIAZ_SAMIR.SEATCATEGORIES";
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.AvailableVenues.Add(new AvailableVenueItem
                                {
                                    SellerCatId = reader.GetDecimal(0),
                                    VenueName = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                                    TotalCapacity = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetDecimal(2))
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
                            SELECT IMTIAZ_SAMIR.seq_venue_booking_requests.NEXTVAL, 
                                   :userId, 
                                   sc.SEATCATID, 
                                   'FULL_VENUE', 
                                   NVL(sc.TOTALSEATS, 1), 
                                   NVL(sc.PRICEPERSEAT * sc.TOTALSEATS, 0), 
                                   'PENDING', 
                                   :notes, 
                                   SYSDATE
                            FROM IMTIAZ_SAMIR.SEATCATEGORIES sc 
                            WHERE sc.SEATCATID = :sellerCatId";

                        cmd.Parameters.Add(new OracleParameter("userId", userId));
                        cmd.Parameters.Add(new OracleParameter("notes", (object?)notes ?? DBNull.Value));
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

        // GET: Customer/MyCommunityPosts
        public async Task<IActionResult> MyCommunityPosts()
        {
            int? userIdNullable = HttpContext.Session.GetInt32("UserId");
            if (userIdNullable == null)
            {
                TempData["ErrorMessage"] = "Please log in first.";
                return RedirectToAction("Login", "Account");
            }
            int userId = userIdNullable.Value;

            string connString = _configuration.GetConnectionString("OracleDbContext")!;
            var myPosts = new List<PostItem>();

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT POST_ID, AUTHOR_ID, AUTHOR_NAME, AUTHOR_ROLE, POST_TYPE, 
                                   TITLE, DESCRIPTION, BUDGET, PRICE, CAPACITY, LOCATION, CREATED_DATE
                            FROM IMTIAZ_SAMIR.VENUE_COMMUNITY_POSTS
                            WHERE AUTHOR_ID = :userId
                            ORDER BY CREATED_DATE DESC";

                        cmd.Parameters.Add(new OracleParameter("userId", userId));

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                myPosts.Add(new PostItem
                                {
                                    PostId = reader.GetDecimal(0),
                                    AuthorId = reader.GetDecimal(1),
                                    AuthorName = reader.GetString(2),
                                    AuthorRole = reader.GetString(3),
                                    PostType = reader.GetString(4),
                                    Title = reader.GetString(5),
                                    Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    Budget = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                                    Price = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                                    Capacity = reader.IsDBNull(9) ? null : Convert.ToInt32(reader.GetDecimal(9)),
                                    Location = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                    CreatedDate = reader.GetDateTime(11)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading your posts: " + ex.Message;
            }

            return View(myPosts);
        }

        // GET: Customer/ContactUs
        [HttpGet]
        public IActionResult ContactUs()
        {
            return View();
        }

        // POST: Customer/ContactUs
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ContactUs(string name, string email, string subject, string message)
        {
            try
            {
                string receiverEmail = "imtiazsamir91@gmail.com";
                TempData["SuccessMessage"] = $"Thank you, {name}! Your message has been sent successfully to {receiverEmail}. We will get back to you soon.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to send message: " + ex.Message;
            }

            return RedirectToAction("ContactUs");
        }

        // GET: Customer/AboutMe
        public async Task<IActionResult> AboutMe()
        {
            int? userIdNullable = HttpContext.Session.GetInt32("UserId");
            if (userIdNullable == null)
            {
                TempData["ErrorMessage"] = "Please log in first.";
                return RedirectToAction("Login", "Account");
            }
            int userId = userIdNullable.Value;

            string connString = _configuration.GetConnectionString("OracleDbContext")!;
            var userProfile = new UserProfileViewModel();

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    string query = @"SELECT u.UserId, u.FullName, u.Email, u.PhoneNumber, u.Address, u.City, r.RoleName
                           FROM IMTIAZ_SAMIR.AppUsers u
                           LEFT JOIN IMTIAZ_SAMIR.AppRoles r ON u.RoleId = r.RoleId
                           WHERE u.UserId = :p_UserId";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        cmd.Parameters.Add("p_UserId", OracleDbType.Decimal).Value = userId;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userProfile.UserId = reader.GetDecimal(0);
                                userProfile.FullName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                userProfile.Email = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                userProfile.PhoneNumber = reader.IsDBNull(3) ? "Not Provided" : reader.GetString(3);
                                userProfile.Address = reader.IsDBNull(4) ? "Not Provided" : reader.GetString(4);
                                userProfile.City = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                userProfile.RoleName = reader.IsDBNull(6) ? "Customer" : reader.GetString(6);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error fetching profile: {Message}", ex.Message);
            }

            return View(userProfile);
        }
    }
}