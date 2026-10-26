using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using EBS.Models;
using Microsoft.AspNetCore.Http;

namespace EBS.Controllers
{
    public class CommunityController : Controller
    {
        private readonly IConfiguration _configuration;

        public CommunityController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ==========================================
        // 1. GET: Customer Timeline (Directly from SellerCategory & SeatCategories)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Timeline()
        {
            var model = new CommunityFeedViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            using (var conn = new OracleConnection(connString))
            {
                await conn.OpenAsync();

                // ১. SellerCategory এবং AppUsers থেকে সঠিক কলাম (ServiceRating) সহ ডাটা ফেচ করা
                string venueQuery = @"
                    SELECT sc.SellerCatId, 
                           sc.SellerId, 
                           u.FullName AS SellerName, 
                           sc.ItemName AS VenueTitle, 
                           sc.ServiceRating,
                           NVL((
                               SELECT SUM(fs.SeatCapacity) 
                               FROM IMTIAZ_SAMIR.ItemFloors f
                               JOIN IMTIAZ_SAMIR.FloorSections fs ON f.FloorId = fs.FloorId
                               WHERE f.SellerCatId = sc.SellerCatId
                           ), 0) AS TotalCapacity,
                           u.Address AS Location
                    FROM IMTIAZ_SAMIR.SellerCategory sc
                    JOIN IMTIAZ_SAMIR.AppUsers u ON sc.SellerId = u.UserId
                    WHERE u.RoleId = 3
                    ORDER BY sc.SellerCatId DESC";

                using (var cmd = new OracleCommand(venueQuery, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            decimal sellerCatId = reader.GetDecimal(0);

                            var post = new PostItem
                            {
                                PostId = sellerCatId,
                                AuthorId = reader.GetDecimal(1),
                                AuthorName = reader.GetString(2),
                                AuthorRole = "SELLER",
                                PostType = "SELLER_VENUE",
                                Title = reader.GetString(3),
                                Description = reader.IsDBNull(4) ? "A beautiful venue perfect for events." : $"Rating: {reader.GetString(4)} - A wonderful venue ready for your booking.",
                                Capacity = Convert.ToInt32(reader.GetDecimal(5)),
                                Location = reader.IsDBNull(6) ? "Dhaka" : reader.GetString(6),
                                CreatedDate = DateTime.Now,
                                Categories = new List<SellerCategoryDetail>()
                            };

                            model.Posts.Add(post);
                        }
                    }
                }

                // ২. প্রতিটি ভেন্যুর জন্য SeatCategories টেবিল থেকে সিট ক্যাটাগরি ও প্রাইস ব্রেকডাউন লোড করা
                foreach (var post in model.Posts)
                {
                    using (var catCmd = conn.CreateCommand())
                    {
                        catCmd.CommandText = @"
                            SELECT CategoryName, TotalSeats, PricePerSeat
                            FROM IMTIAZ_SAMIR.SeatCategories
                            WHERE SellerCatId = :p_SellerCatId
                            ORDER BY SeatCatId ASC";

                        catCmd.Parameters.Add(new OracleParameter("p_SellerCatId", post.PostId));

                        using (var catReader = await catCmd.ExecuteReaderAsync())
                        {
                            while (await catReader.ReadAsync())
                            {
                                post.Categories.Add(new SellerCategoryDetail
                                {
                                    CategoryName = catReader.GetString(0),
                                    TotalSeats = Convert.ToInt32(catReader.GetDecimal(1)),
                                    PricePerSeat = catReader.GetDecimal(2)
                                });
                            }
                        }
                    }
                }
            }

            return View(model);
        }

        // ==========================================
        // 2. GET: Seller Timeline (Customer Requirements)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> SellerTimeline()
        {
            var model = new CommunityFeedViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            using (var conn = new OracleConnection(connString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT p.POST_ID, p.AUTHOR_ID, NVL(u.FullName, p.AUTHOR_NAME) AS AUTHOR_NAME, 
                               p.AUTHOR_ROLE, p.POST_TYPE, p.TITLE, p.DESCRIPTION, 
                               p.BUDGET, p.PRICE, p.CAPACITY, p.LOCATION, p.CREATED_DATE
                        FROM IMTIAZ_SAMIR.VENUE_COMMUNITY_POSTS p
                        LEFT JOIN IMTIAZ_SAMIR.AppUsers u ON p.AUTHOR_ID = u.UserId
                        WHERE p.POST_TYPE = 'USER_REQUIREMENT'
                        ORDER BY p.CREATED_DATE DESC";

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            model.Posts.Add(new PostItem
                            {
                                PostId = reader.GetDecimal(0),
                                AuthorId = reader.GetDecimal(1),
                                AuthorName = reader.GetString(2),
                                AuthorRole = reader.GetString(3),
                                PostType = reader.GetString(4),
                                Title = reader.GetString(5),
                                Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                Budget = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7),
                                Price = reader.IsDBNull(8) ? (decimal?)null : reader.GetDecimal(8),
                                Capacity = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                                Location = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                CreatedDate = reader.GetDateTime(11)
                            });
                        }
                    }
                }
            }

            return View("Timeline", model);
        }

        // ==========================================
        // 3. POST: User Creates a Requirement Post
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUserPost(CommunityFeedViewModel model)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            int userId = sessionUserId.HasValue ? sessionUserId.Value : 6;
            string userName = HttpContext.Session.GetString("UserName") ?? "Customer";

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            using (var conn = new OracleConnection(connString))
            {
                await conn.OpenAsync();

                if (userName == "Customer" && userId > 0)
                {
                    string nameQuery = "SELECT FullName FROM IMTIAZ_SAMIR.AppUsers WHERE UserId = :p_Id";
                    using (var nCmd = new OracleCommand(nameQuery, conn))
                    {
                        nCmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = userId;
                        var res = await nCmd.ExecuteScalarAsync();
                        if (res != null && res != DBNull.Value) userName = res.ToString()!;
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO IMTIAZ_SAMIR.VENUE_COMMUNITY_POSTS 
                        (POST_ID, AUTHOR_ID, AUTHOR_NAME, AUTHOR_ROLE, POST_TYPE, TITLE, DESCRIPTION, BUDGET, CAPACITY, LOCATION, CREATED_DATE)
                        VALUES 
                        (IMTIAZ_SAMIR.seq_community_posts.NEXTVAL, :authorId, :authorName, 'CUSTOMER', 'USER_REQUIREMENT', :title, :description, :budget, :capacity, :location, SYSDATE)";

                    cmd.Parameters.Add(new OracleParameter("authorId", userId));
                    cmd.Parameters.Add(new OracleParameter("authorName", userName));
                    cmd.Parameters.Add(new OracleParameter("title", model.NewPost.Title));
                    cmd.Parameters.Add(new OracleParameter("description", model.NewPost.Description ?? (object)DBNull.Value));
                    cmd.Parameters.Add(new OracleParameter("budget", model.NewPost.Budget ?? (object)DBNull.Value));
                    cmd.Parameters.Add(new OracleParameter("capacity", model.NewPost.Capacity ?? (object)DBNull.Value));
                    cmd.Parameters.Add(new OracleParameter("location", model.NewPost.Location ?? (object)DBNull.Value));

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["SuccessMessage"] = "Your venue requirement post has been published successfully!";
            return RedirectToAction(nameof(Timeline));
        }
    }
}