using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using EBS.Models;

namespace EBS.Controllers
{
    public class CommunityController : Controller
    {
        private readonly IConfiguration _configuration;

        public CommunityController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // GET: Facebook-style Timeline Feed
        public async Task<IActionResult> Timeline()
        {
            var model = new CommunityFeedViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            using (var conn = new OracleConnection(connString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT POST_ID, AUTHOR_ID, AUTHOR_NAME, AUTHOR_ROLE, POST_TYPE, 
                               TITLE, DESCRIPTION, BUDGET, PRICE, CAPACITY, LOCATION, CREATED_DATE
                        FROM IMTIAZ_SAMIR.VENUE_COMMUNITY_POSTS
                        ORDER BY CREATED_DATE DESC";

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

            return View(model);
        }

        // POST: User creates a requirement post (কী ধরনের ভেন্যু লাগবে)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUserPost(CommunityFeedViewModel model)
        {
            int userId = 1; // বর্তমান লগইন করা ইউজারের আইডি
            string userName = "Imtiaz Rahman"; // ইউজারের নাম

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            using (var conn = new OracleConnection(connString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO IMTIAZ_SAMIR.VENUE_COMMUNITY_POSTS 
                        (POST_ID, AUTHOR_ID, AUTHOR_NAME, AUTHOR_ROLE, POST_TYPE, TITLE, DESCRIPTION, BUDGET, CAPACITY, LOCATION)
                        VALUES (IMTIAZ_SAMIR.seq_community_posts.NEXTVAL, :authorId, :authorName, 'CUSTOMER', 'USER_REQUIREMENT', :title, :description, :budget, :capacity, :location)";

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

            TempData["SuccessMessage"] = "Your venue requirement post has been published to the timeline!";
            return RedirectToAction(nameof(Timeline));
        }
    }
}