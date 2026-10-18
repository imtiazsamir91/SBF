using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        // ==========================================
        // 1. DASHBOARD ACTIONS
        // ==========================================

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
                        cmd.BindByName = true;
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

        // ==========================================
        // 2. CATEGORY MANAGEMENT ACTIONS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var model = new CategoriesPageViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    string query = @"SELECT CategoryId, CategoryName, Description, CreatedAt 
                                   FROM IMTIAZ_SAMIR.Categories 
                                   ORDER BY CategoryId DESC";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.CategoriesList.Add(new CategoryViewModel
                                {
                                    CategoryId = reader.GetDecimal(0),
                                    CategoryName = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    CreatedAt = reader.IsDBNull(3) ? DateTime.Now : reader.GetDateTime(3)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Categories Fetch Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to load categories: " + ex.Message;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CategoriesPageViewModel inputModel)
        {
            var category = inputModel.NewCategory;

            if (category == null || string.IsNullOrWhiteSpace(category.CategoryName))
            {
                TempData["ErrorMessage"] = "Category Name is required.";
                return RedirectToAction(nameof(Categories));
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    string insertQuery = @"INSERT INTO IMTIAZ_SAMIR.Categories (CategoryName, Description) 
                                           VALUES (:p_Name, :p_Desc)";

                    using (var cmd = new OracleCommand(insertQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = category.CategoryName.Trim();
                        cmd.Parameters.Add("p_Desc", OracleDbType.Varchar2).Value = (object?)category.Description ?? DBNull.Value;

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = $"Category '{category.CategoryName}' created successfully!";
            }
            catch (OracleException ex) when (ex.Number == 1)
            {
                _logger.LogError("❌ Duplicate Category Name: {Name}", category.CategoryName);
                TempData["ErrorMessage"] = $"A category named '{category.CategoryName}' already exists!";
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Create Category Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to create category: " + ex.Message;
            }

            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(decimal categoryId, string categoryName, string description)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                TempData["ErrorMessage"] = "Category Name is required.";
                return RedirectToAction(nameof(Categories));
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    string updateQuery = @"UPDATE IMTIAZ_SAMIR.Categories 
                                           SET CategoryName = :p_Name, Description = :p_Desc 
                                           WHERE CategoryId = :p_Id";

                    using (var cmd = new OracleCommand(updateQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = categoryName.Trim();
                        cmd.Parameters.Add("p_Desc", OracleDbType.Varchar2).Value = (object?)description ?? DBNull.Value;
                        cmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = categoryId;

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Category updated successfully!";
            }
            catch (OracleException ex) when (ex.Number == 1)
            {
                TempData["ErrorMessage"] = $"A category named '{categoryName}' already exists!";
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Edit Category Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to update category: " + ex.Message;
            }

            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(decimal categoryId)
        {
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    string deleteQuery = "DELETE FROM IMTIAZ_SAMIR.Categories WHERE CategoryId = :p_Id";

                    using (var cmd = new OracleCommand(deleteQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = categoryId;
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Category deleted successfully!";
            }
            catch (OracleException ex) when (ex.Number == 2292)
            {
                TempData["ErrorMessage"] = "Cannot delete this category because service items are linked to it.";
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Delete Category Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to delete category: " + ex.Message;
            }

            return RedirectToAction(nameof(Categories));
        }

        // ==========================================
        // 3. SELLER MANAGEMENT ACTIONS (With Area/Endpoint Filter)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Sellers(string? searchKeyword, decimal? categoryFilter, string? ratingFilter, string? floorFilter)
        {
            var model = new SellersPageViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // 1. Load Categories Dropdown
                    string catQuery = "SELECT CategoryId, CategoryName FROM IMTIAZ_SAMIR.Categories ORDER BY CategoryName ASC";
                    using (var catCmd = new OracleCommand(catQuery, conn))
                    {
                        using (var reader = await catCmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.NewSeller.CategoriesDropdown.Add(new SelectListItem
                                {
                                    Value = reader.GetDecimal(0).ToString(),
                                    Text = reader.GetString(1)
                                });
                            }
                        }
                    }

                    // 2. Dynamic Search & Multi-Filter Query (Search, Category, Rating, Floors & Seats)
                    string sellerQuery = @"SELECT u.UserId, u.FullName, u.Email, u.PhoneNumber, 
                                        u.Area, u.Thana, u.District, u.PostCode, NVL(u.Status, 'Active') as Status,
                                        NVL(sc.ItemName, 'N/A'), NVL(c.CategoryName, 'N/A'), NVL(sc.ServiceRating, '5 Star') as Rating,
                                        sc.CategoryId,
                                        (SELECT COUNT(*) FROM IMTIAZ_SAMIR.ItemFloors f WHERE f.SellerCatId = sc.SellerCatId) as TotalFloors,
                                        (SELECT NVL(SUM(fs.SeatCapacity), 0) 
                                         FROM IMTIAZ_SAMIR.ItemFloors f 
                                         JOIN IMTIAZ_SAMIR.FloorSections fs ON f.FloorId = fs.FloorId 
                                         WHERE f.SellerCatId = sc.SellerCatId) as TotalSeats
                                  FROM IMTIAZ_SAMIR.AppUsers u
                                  LEFT JOIN IMTIAZ_SAMIR.SellerCategory sc ON u.UserId = sc.SellerId
                                  LEFT JOIN IMTIAZ_SAMIR.Categories c ON sc.CategoryId = c.CategoryId
                                  WHERE u.RoleId = 3";

                    // Multi-Field Keyword Search
                    if (!string.IsNullOrWhiteSpace(searchKeyword))
                    {
                        sellerQuery += @" AND (LOWER(u.FullName) LIKE :p_Search 
                                   OR LOWER(u.Email) LIKE :p_Search 
                                   OR LOWER(u.PhoneNumber) LIKE :p_Search 
                                   OR LOWER(u.Area) LIKE :p_Search 
                                   OR LOWER(u.Thana) LIKE :p_Search 
                                   OR LOWER(u.District) LIKE :p_Search 
                                   OR LOWER(sc.ItemName) LIKE :p_Search)";
                    }

                    // Category Filter
                    if (categoryFilter.HasValue && categoryFilter.Value > 0)
                    {
                        sellerQuery += " AND sc.CategoryId = :p_CatId";
                    }

                    // Service Rating Filter
                    if (!string.IsNullOrWhiteSpace(ratingFilter))
                    {
                        sellerQuery += " AND LOWER(sc.ServiceRating) = :p_Rating";
                    }

                    // Floors & Seats Filter
                    if (floorFilter == "has_floors")
                    {
                        sellerQuery += " AND (SELECT COUNT(*) FROM IMTIAZ_SAMIR.ItemFloors f WHERE f.SellerCatId = sc.SellerCatId) > 0";
                    }
                    else if (floorFilter == "no_floors")
                    {
                        sellerQuery += " AND (SELECT COUNT(*) FROM IMTIAZ_SAMIR.ItemFloors f WHERE f.SellerCatId = sc.SellerCatId) = 0";
                    }

                    sellerQuery += " ORDER BY u.UserId DESC";

                    using (var cmd = new OracleCommand(sellerQuery, conn))
                    {
                        cmd.BindByName = true;

                        if (!string.IsNullOrWhiteSpace(searchKeyword))
                        {
                            cmd.Parameters.Add("p_Search", OracleDbType.Varchar2).Value = $"%{searchKeyword.Trim().ToLower()}%";
                        }

                        if (categoryFilter.HasValue && categoryFilter.Value > 0)
                        {
                            cmd.Parameters.Add("p_CatId", OracleDbType.Decimal).Value = categoryFilter.Value;
                        }

                        if (!string.IsNullOrWhiteSpace(ratingFilter))
                        {
                            cmd.Parameters.Add("p_Rating", OracleDbType.Varchar2).Value = ratingFilter.Trim().ToLower();
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string area = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                string thana = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                string dist = reader.IsDBNull(6) ? "" : reader.GetString(6);
                                string location = string.Join(", ", new[] { area, thana, dist }.Where(x => !string.IsNullOrWhiteSpace(x)));

                                model.SellersList.Add(new SellerListItemDto
                                {
                                    UserId = reader.GetDecimal(0),
                                    FullName = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                                    Email = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                                    PhoneNumber = reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                                    Location = string.IsNullOrWhiteSpace(location) ? "N/A" : location,
                                    Status = reader.IsDBNull(8) ? "Active" : reader.GetString(8),
                                    ItemName = reader.IsDBNull(9) ? "N/A" : reader.GetString(9),
                                    CategoryName = reader.IsDBNull(10) ? "N/A" : reader.GetString(10),
                                    ServiceRating = reader.IsDBNull(11) ? "5 Star" : reader.GetString(11),
                                    TotalFloors = reader.IsDBNull(13) ? 0 : Convert.ToInt32(reader[13]),
                                    TotalSeats = reader.IsDBNull(14) ? 0 : Convert.ToInt32(reader[14])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Load Sellers Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to load sellers: " + ex.Message;
            }

            ViewBag.SearchKeyword = searchKeyword;
            ViewBag.CategoryFilter = categoryFilter;
            ViewBag.RatingFilter = ratingFilter;
            ViewBag.FloorFilter = floorFilter;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSeller(SellersPageViewModel inputModel)
        {
            var s = inputModel?.NewSeller;
            if (s == null)
            {
                TempData["ErrorMessage"] = "Form submission failed. Invalid data received.";
                return RedirectToAction(nameof(Sellers));
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // 1. Insert User into AppUsers
                    string insertUserQuery = @"INSERT INTO IMTIAZ_SAMIR.AppUsers 
            (UserId, FullName, Email, PasswordHash, RoleId, PhoneNumber, Address, Area, Thana, District, PostCode, Status) 
            VALUES 
            (IMTIAZ_SAMIR.seq_users.NEXTVAL, :p_Name, :p_Email, :p_Pwd, 3, :p_Phone, :p_Addr, :p_Area, :p_Thana, :p_Dist, :p_Post, :p_Status)";

                    using (var cmd = new OracleCommand(insertUserQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = s.FullName?.Trim() ?? "";
                        cmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = s.Email?.Trim() ?? "";
                        cmd.Parameters.Add("p_Pwd", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(s.Password) ? "123456" : s.Password;
                        cmd.Parameters.Add("p_Phone", OracleDbType.Varchar2).Value = s.PhoneNumber ?? "";
                        cmd.Parameters.Add("p_Addr", OracleDbType.Varchar2).Value = (object?)s.Address ?? DBNull.Value;
                        cmd.Parameters.Add("p_Area", OracleDbType.Varchar2).Value = (object?)s.Area ?? DBNull.Value;
                        cmd.Parameters.Add("p_Thana", OracleDbType.Varchar2).Value = (object?)s.Thana ?? DBNull.Value;
                        cmd.Parameters.Add("p_Dist", OracleDbType.Varchar2).Value = (object?)s.District ?? DBNull.Value;
                        cmd.Parameters.Add("p_Post", OracleDbType.Varchar2).Value = (object?)s.PostCode ?? DBNull.Value;
                        cmd.Parameters.Add("p_Status", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(s.Status) ? "Active" : s.Status;

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Retrieve the newly created UserId
                    decimal sellerId = 0;
                    using (var idCmd = new OracleCommand("SELECT UserId FROM IMTIAZ_SAMIR.AppUsers WHERE Email = :p_Email", conn))
                    {
                        idCmd.BindByName = true;
                        idCmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = s.Email.Trim();
                        var res = await idCmd.ExecuteScalarAsync();
                        if (res != null) sellerId = Convert.ToDecimal(res);
                    }

                    // 3. Insert into SellerCategory
                    decimal sellerCatId = 0;
                    if (sellerId > 0 && !string.IsNullOrWhiteSpace(s.ItemName))
                    {
                        string insertSellerCat = @"INSERT INTO IMTIAZ_SAMIR.SellerCategory 
            (SellerCatId, CategoryId, SellerId, ItemName, ServiceRating, HasMultipleFloors) 
            VALUES 
            (IMTIAZ_SAMIR.seq_seller_category.NEXTVAL, :p_CatId, :p_SellerId, :p_ItemName, :p_Rating, :p_Multi)";

                        using (var scCmd = new OracleCommand(insertSellerCat, conn))
                        {
                            scCmd.BindByName = true;
                            scCmd.Parameters.Add("p_CatId", OracleDbType.Decimal).Value = s.CategoryId;
                            scCmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;
                            scCmd.Parameters.Add("p_ItemName", OracleDbType.Varchar2).Value = s.ItemName.Trim();
                            scCmd.Parameters.Add("p_Rating", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(s.ServiceRating) ? "5 Star" : s.ServiceRating;
                            scCmd.Parameters.Add("p_Multi", OracleDbType.Decimal).Value = s.HasMultipleFloors ? 1 : 0;

                            await scCmd.ExecuteNonQueryAsync();
                        }

                        using (var scIdCmd = new OracleCommand("SELECT MAX(SellerCatId) FROM IMTIAZ_SAMIR.SellerCategory WHERE SellerId = :p_SellerId", conn))
                        {
                            scIdCmd.BindByName = true;
                            scIdCmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;
                            var resSc = await scIdCmd.ExecuteScalarAsync();
                            if (resSc != null && resSc != DBNull.Value) sellerCatId = Convert.ToDecimal(resSc);
                        }
                    }

                    // 4. Save Dynamic Floors & Sections
                    if (sellerCatId > 0 && s.Floors != null && s.Floors.Any())
                    {
                        foreach (var fl in s.Floors)
                        {
                            if (fl == null) continue;

                            decimal floorId = 0;
                            string insertFloor = @"INSERT INTO IMTIAZ_SAMIR.ItemFloors (FloorId, SellerCatId, FloorNumber, FloorName) 
                                        VALUES (IMTIAZ_SAMIR.seq_item_floors.NEXTVAL, :p_ScId, :p_Num, :p_Name)";

                            using (var flCmd = new OracleCommand(insertFloor, conn))
                            {
                                flCmd.BindByName = true;
                                flCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;
                                flCmd.Parameters.Add("p_Num", OracleDbType.Decimal).Value = fl.FloorNumber;
                                flCmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(fl.FloorName) ? $"Floor {fl.FloorNumber}" : fl.FloorName;

                                await flCmd.ExecuteNonQueryAsync();
                            }

                            using (var flIdCmd = new OracleCommand("SELECT MAX(FloorId) FROM IMTIAZ_SAMIR.ItemFloors WHERE SellerCatId = :p_ScId", conn))
                            {
                                flIdCmd.BindByName = true;
                                flIdCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;
                                var resFl = await flIdCmd.ExecuteScalarAsync();
                                if (resFl != null && resFl != DBNull.Value) floorId = Convert.ToDecimal(resFl);
                            }

                            if (floorId > 0 && fl.Sections != null)
                            {
                                foreach (var sec in fl.Sections)
                                {
                                    if (sec == null || string.IsNullOrWhiteSpace(sec.SectionName)) continue;

                                    string insertSec = @"INSERT INTO IMTIAZ_SAMIR.FloorSections (SectionId, FloorId, SectionName, SeatCapacity) 
                                                 VALUES (IMTIAZ_SAMIR.seq_floor_sections.NEXTVAL, :p_FlId, :p_SecName, :p_Seats)";

                                    using (var secCmd = new OracleCommand(insertSec, conn))
                                    {
                                        secCmd.BindByName = true;
                                        secCmd.Parameters.Add("p_FlId", OracleDbType.Decimal).Value = floorId;
                                        secCmd.Parameters.Add("p_SecName", OracleDbType.Varchar2).Value = sec.SectionName;
                                        secCmd.Parameters.Add("p_Seats", OracleDbType.Decimal).Value = sec.SeatCapacity;

                                        await secCmd.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                        }
                    }

                    TempData["SuccessMessage"] = $"Seller '{s.FullName}' registered successfully with floors & sections!";
                }
            }
            catch (OracleException ex) when (ex.Number == 1)
            {
                TempData["ErrorMessage"] = $"Email '{s.Email}' is already registered in the system!";
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Create Seller Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to create seller: " + ex.Message;
            }

            return RedirectToAction(nameof(Sellers));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSeller(decimal userId, string fullName, string email, string phoneNumber,
                                           string area, string thana, string district, string postCode,
                                           string itemName, decimal categoryId, string serviceRating, string status,
                                           List<FloorInputDto>? Floors)
        {
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // 1. AppUsers আপডেট
                    string updateUser = @"UPDATE IMTIAZ_SAMIR.AppUsers 
                                  SET FullName = :p_Name, Email = :p_Email, PhoneNumber = :p_Phone, 
                                      Area = :p_Area, Thana = :p_Thana, District = :p_Dist, PostCode = :p_Post, Status = :p_Status
                                  WHERE UserId = :p_Id AND RoleId = 3";

                    using (var cmd = new OracleCommand(updateUser, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = fullName.Trim();
                        cmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = email.Trim();
                        cmd.Parameters.Add("p_Phone", OracleDbType.Varchar2).Value = phoneNumber.Trim();
                        cmd.Parameters.Add("p_Area", OracleDbType.Varchar2).Value = (object?)area ?? DBNull.Value;
                        cmd.Parameters.Add("p_Thana", OracleDbType.Varchar2).Value = (object?)thana ?? DBNull.Value;
                        cmd.Parameters.Add("p_Dist", OracleDbType.Varchar2).Value = (object?)district ?? DBNull.Value;
                        cmd.Parameters.Add("p_Post", OracleDbType.Varchar2).Value = (object?)postCode ?? DBNull.Value;
                        cmd.Parameters.Add("p_Status", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(status) ? "Active" : status;
                        cmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = userId;

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. SellerCategory আপডেট
                    decimal sellerCatId = 0;
                    using (var scIdCmd = new OracleCommand("SELECT SellerCatId FROM IMTIAZ_SAMIR.SellerCategory WHERE SellerId = :p_Id", conn))
                    {
                        scIdCmd.BindByName = true;
                        scIdCmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = userId;
                        var res = await scIdCmd.ExecuteScalarAsync();
                        if (res != null && res != DBNull.Value) sellerCatId = Convert.ToDecimal(res);
                    }

                    if (sellerCatId > 0)
                    {
                        string updateCat = @"UPDATE IMTIAZ_SAMIR.SellerCategory 
                                     SET ItemName = :p_ItemName, CategoryId = :p_CatId, ServiceRating = :p_Rating 
                                     WHERE SellerCatId = :p_ScId";

                        using (var scCmd = new OracleCommand(updateCat, conn))
                        {
                            scCmd.BindByName = true;
                            scCmd.Parameters.Add("p_ItemName", OracleDbType.Varchar2).Value = itemName.Trim();
                            scCmd.Parameters.Add("p_CatId", OracleDbType.Decimal).Value = categoryId;
                            scCmd.Parameters.Add("p_Rating", OracleDbType.Varchar2).Value = serviceRating;
                            scCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;

                            await scCmd.ExecuteNonQueryAsync();
                        }

                        // 3. পুরোনো Floors & Sections ডিলিট করা
                        using (var delSecCmd = new OracleCommand("DELETE FROM IMTIAZ_SAMIR.FloorSections WHERE FloorId IN (SELECT FloorId FROM IMTIAZ_SAMIR.ItemFloors WHERE SellerCatId = :p_ScId)", conn))
                        {
                            delSecCmd.BindByName = true;
                            delSecCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;
                            await delSecCmd.ExecuteNonQueryAsync();
                        }

                        using (var delFlCmd = new OracleCommand("DELETE FROM IMTIAZ_SAMIR.ItemFloors WHERE SellerCatId = :p_ScId", conn))
                        {
                            delFlCmd.BindByName = true;
                            delFlCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;
                            await delFlCmd.ExecuteNonQueryAsync();
                        }

                        // 4. নতুন/আপডেটেড Floors & Sections ইনসার্ট
                        if (Floors != null && Floors.Any())
                        {
                            foreach (var fl in Floors)
                            {
                                if (fl == null) continue;

                                decimal floorId = 0;
                                string insertFloor = @"INSERT INTO IMTIAZ_SAMIR.ItemFloors (FloorId, SellerCatId, FloorNumber, FloorName) 
                                               VALUES (IMTIAZ_SAMIR.seq_item_floors.NEXTVAL, :p_ScId, :p_Num, :p_Name)";

                                using (var flCmd = new OracleCommand(insertFloor, conn))
                                {
                                    flCmd.BindByName = true;
                                    flCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;
                                    flCmd.Parameters.Add("p_Num", OracleDbType.Decimal).Value = fl.FloorNumber;
                                    flCmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(fl.FloorName) ? $"Floor {fl.FloorNumber}" : fl.FloorName;

                                    await flCmd.ExecuteNonQueryAsync();
                                }

                                using (var flIdCmd = new OracleCommand("SELECT MAX(FloorId) FROM IMTIAZ_SAMIR.ItemFloors WHERE SellerCatId = :p_ScId", conn))
                                {
                                    flIdCmd.BindByName = true;
                                    flIdCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;
                                    var resFl = await flIdCmd.ExecuteScalarAsync();
                                    if (resFl != null && resFl != DBNull.Value) floorId = Convert.ToDecimal(resFl);
                                }

                                if (floorId > 0 && fl.Sections != null)
                                {
                                    foreach (var sec in fl.Sections)
                                    {
                                        if (sec == null || string.IsNullOrWhiteSpace(sec.SectionName)) continue;

                                        string insertSec = @"INSERT INTO IMTIAZ_SAMIR.FloorSections (SectionId, FloorId, SectionName, SeatCapacity) 
                                                     VALUES (IMTIAZ_SAMIR.seq_floor_sections.NEXTVAL, :p_FlId, :p_SecName, :p_Seats)";

                                        using (var secCmd = new OracleCommand(insertSec, conn))
                                        {
                                            secCmd.BindByName = true;
                                            secCmd.Parameters.Add("p_FlId", OracleDbType.Decimal).Value = floorId;
                                            secCmd.Parameters.Add("p_SecName", OracleDbType.Varchar2).Value = sec.SectionName;
                                            secCmd.Parameters.Add("p_Seats", OracleDbType.Decimal).Value = sec.SeatCapacity;

                                            await secCmd.ExecuteNonQueryAsync();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                TempData["SuccessMessage"] = "Seller information and floors updated successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Update Seller Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to update seller: " + ex.Message;
            }

            TempData["ActiveTab"] = "list";
            return RedirectToAction(nameof(Sellers));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSeller(decimal userId)
        {
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    string deleteCatQuery = "DELETE FROM IMTIAZ_SAMIR.SellerCategory WHERE SellerId = :p_UserId";
                    using (var cmd1 = new OracleCommand(deleteCatQuery, conn))
                    {
                        cmd1.BindByName = true;
                        cmd1.Parameters.Add("p_UserId", OracleDbType.Decimal).Value = userId;
                        await cmd1.ExecuteNonQueryAsync();
                    }

                    string deleteUserQuery = "DELETE FROM IMTIAZ_SAMIR.AppUsers WHERE UserId = :p_UserId AND RoleId = 3";
                    using (var cmd2 = new OracleCommand(deleteUserQuery, conn))
                    {
                        cmd2.BindByName = true;
                        cmd2.Parameters.Add("p_UserId", OracleDbType.Decimal).Value = userId;
                        await cmd2.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Seller deleted successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Delete Seller Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Cannot delete seller because active floors or bookings exist.";
            }

            TempData["ActiveTab"] = "list"; // Keep user in List view after delete!
            return RedirectToAction(nameof(Sellers));
        }

        [HttpGet]
        public async Task<IActionResult> GetSellerFloors(decimal userId)
        {
            string connString = _configuration.GetConnectionString("OracleDbContext")!;
            var floors = new List<object>();

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    string query = @"SELECT f.FloorId, f.FloorNumber, f.FloorName, 
                                   s.SectionId, s.SectionName, s.SeatCapacity
                            FROM IMTIAZ_SAMIR.SellerCategory sc
                            JOIN IMTIAZ_SAMIR.ItemFloors f ON sc.SellerCatId = f.SellerCatId
                            LEFT JOIN IMTIAZ_SAMIR.FloorSections s ON f.FloorId = s.FloorId
                            WHERE sc.SellerId = :p_UserId
                            ORDER BY f.FloorNumber ASC, s.SectionId ASC";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_UserId", OracleDbType.Decimal).Value = userId;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            Dictionary<decimal, dynamic> floorDict = new Dictionary<decimal, dynamic>();

                            while (await reader.ReadAsync())
                            {
                                decimal floorId = reader.GetDecimal(0);
                                int floorNum = Convert.ToInt32(reader[1]);
                                string floorName = reader.GetString(2);

                                if (!floorDict.ContainsKey(floorId))
                                {
                                    floorDict[floorId] = new
                                    {
                                        floorId = floorId,
                                        floorNumber = floorNum,
                                        floorName = floorName,
                                        sections = new List<object>()
                                    };
                                }

                                if (!reader.IsDBNull(3))
                                {
                                    floorDict[floorId].sections.Add(new
                                    {
                                        sectionId = reader.GetDecimal(3),
                                        sectionName = reader.GetString(4),
                                        seatCapacity = Convert.ToInt32(reader[5])
                                    });
                                }
                            }

                            floors = floorDict.Values.ToList();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ GetSellerFloors Error: {Message}", ex.Message);
            }

            return Json(floors);
        }
    }
}