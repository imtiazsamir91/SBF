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
                        cmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = categoryId;
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Category deleted successfully!";
            }
            catch (OracleException ex) when (ex.Number == 2292) // Foreign key constraint violation (Child records exist)
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
        // 3. SELLER MANAGEMENT ACTIONS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Sellers()
        {
            var model = new SellersPageViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // 1. Load Categories for Dropdown Selection
                    string catQuery = "SELECT CategoryId, CategoryName FROM IMTIAZ_SAMIR.Categories ORDER BY CategoryName ASC";
                    using (var catCmd = new OracleCommand(catQuery, conn))
                    {
                        using (var reader = await catCmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.NewSeller.CategoriesDropdown.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                                {
                                    Value = reader.GetDecimal(0).ToString(),
                                    Text = reader.GetString(1)
                                });
                            }
                        }
                    }

                    // 2. Load Existing Sellers (RoleId = 3)
                    string sellerQuery = @"SELECT u.UserId, u.FullName, u.Email, u.PhoneNumber, 
                                          u.Area || ', ' || u.Thana || ', ' || u.District as Location,
                                          i.ItemName, c.CategoryName, NVL(i.ServiceRating, 'N/A'), NVL(i.Capacity, 0), NVL(i.BasePrice, 0)
                                   FROM IMTIAZ_SAMIR.AppUsers u
                                   LEFT JOIN IMTIAZ_SAMIR.CategoryItems i ON u.UserId = i.SellerId
                                   LEFT JOIN IMTIAZ_SAMIR.Categories c ON i.CategoryId = c.CategoryId
                                   WHERE u.RoleId = 3
                                   ORDER BY u.UserId DESC";

                    using (var cmd = new OracleCommand(sellerQuery, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.SellersList.Add(new SellerListItemDto
                                {
                                    UserId = reader.GetDecimal(0),
                                    FullName = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                                    Email = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                                    PhoneNumber = reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                                    Location = reader.IsDBNull(4) ? "N/A" : reader.GetString(4),
                                    ItemName = reader.IsDBNull(5) ? "N/A" : reader.GetString(5),
                                    CategoryName = reader.IsDBNull(6) ? "N/A" : reader.GetString(6),
                                    ServiceRating = reader.IsDBNull(7) ? "N/A" : reader.GetString(7),
                                    Capacity = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader[8]),
                                    BasePrice = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Load Sellers Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to load seller records: " + ex.Message;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSeller(SellersPageViewModel inputModel)
        {
            var s = inputModel.NewSeller;
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // 1. Insert Seller into AppUsers (RoleId = 3)
                    string insertUserQuery = @"INSERT INTO IMTIAZ_SAMIR.AppUsers 
                (UserId, FullName, Email, PasswordHash, RoleId, PhoneNumber, Address, Area, Thana, District, PostCode) 
                VALUES 
                (IMTIAZ_SAMIR.seq_users.NEXTVAL, :p_Name, :p_Email, :p_Pwd, 3, :p_Phone, :p_Addr, :p_Area, :p_Thana, :p_Dist, :p_Post)";

                    using (var cmd = new OracleCommand(insertUserQuery, conn))
                    {
                        cmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = s.FullName.Trim();
                        cmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = s.Email.Trim();
                        cmd.Parameters.Add("p_Pwd", OracleDbType.Varchar2).Value = s.Password; // Production-এ Password Hash দেওয়া উচিত
                        cmd.Parameters.Add("p_Phone", OracleDbType.Varchar2).Value = s.PhoneNumber;
                        cmd.Parameters.Add("p_Addr", OracleDbType.Varchar2).Value = (object?)s.Address ?? DBNull.Value;
                        cmd.Parameters.Add("p_Area", OracleDbType.Varchar2).Value = (object?)s.Area ?? DBNull.Value;
                        cmd.Parameters.Add("p_Thana", OracleDbType.Varchar2).Value = (object?)s.Thana ?? DBNull.Value;
                        cmd.Parameters.Add("p_Dist", OracleDbType.Varchar2).Value = (object?)s.District ?? DBNull.Value;
                        cmd.Parameters.Add("p_Post", OracleDbType.Varchar2).Value = (object?)s.PostCode ?? DBNull.Value;

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Fetch Newly Created Seller ID
                    decimal createdSellerId = 0;
                    using (var idCmd = new OracleCommand("SELECT UserId FROM IMTIAZ_SAMIR.AppUsers WHERE Email = :p_Email", conn))
                    {
                        idCmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = s.Email.Trim();
                        var res = await idCmd.ExecuteScalarAsync();
                        if (res != null) createdSellerId = Convert.ToDecimal(res);
                    }

                    // 3. Create Service Item Linked with Category and Seller
                    if (createdSellerId > 0 && !string.IsNullOrWhiteSpace(s.ItemName))
                    {
                        string insertItemQuery = @"INSERT INTO IMTIAZ_SAMIR.CategoryItems 
                    (ItemId, CategoryId, SellerId, ItemName, Description, BasePrice, Capacity, ServiceRating) 
                    VALUES 
                    (IMTIAZ_SAMIR.seq_category_items.NEXTVAL, :p_CatId, :p_SellerId, :p_ItemName, :p_Desc, :p_Price, :p_Cap, :p_Rating)";

                        using (var itemCmd = new OracleCommand(insertItemQuery, conn))
                        {
                            itemCmd.Parameters.Add("p_CatId", OracleDbType.Decimal).Value = s.CategoryId;
                            itemCmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = createdSellerId;
                            itemCmd.Parameters.Add("p_ItemName", OracleDbType.Varchar2).Value = s.ItemName.Trim();
                            itemCmd.Parameters.Add("p_Desc", OracleDbType.Varchar2).Value = (object?)s.Description ?? DBNull.Value;
                            itemCmd.Parameters.Add("p_Price", OracleDbType.Decimal).Value = s.BasePrice;
                            itemCmd.Parameters.Add("p_Cap", OracleDbType.Decimal).Value = s.Capacity;
                            itemCmd.Parameters.Add("p_Rating", OracleDbType.Varchar2).Value = s.ServiceRating;

                            await itemCmd.ExecuteNonQueryAsync();
                        }
                    }

                    TempData["SuccessMessage"] = $"Seller '{s.FullName}' and Service Item registered successfully!";
                }
            }
            catch (OracleException ex) when (ex.Number == 1) // ORA-00001 Unique Email Constraint
            {
                TempData["ErrorMessage"] = $"A user or seller with email '{s.Email}' already exists!";
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Create Seller Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to create seller: " + ex.Message;
            }

            return RedirectToAction(nameof(Sellers));
        }
        // ==========================================
        // SELLER MANAGEMENT (SellerCategory Table)
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Sellers()
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

                    // 2. Load Sellers List with Aggregated Floor & Seat Counts
                    string sellerQuery = @"SELECT u.UserId, u.FullName, u.Email, u.PhoneNumber, 
                                          u.Area || ', ' || u.Thana || ', ' || u.District as Location,
                                          NVL(sc.ItemName, 'N/A'), NVL(c.CategoryName, 'N/A'), NVL(sc.ServiceRating, 'N/A'),
                                          (SELECT COUNT(*) FROM IMTIAZ_SAMIR.ItemFloors f WHERE f.SellerCatId = sc.SellerCatId) as TotalFloors,
                                          (SELECT NVL(SUM(fs.SeatCapacity), 0) 
                                           FROM IMTIAZ_SAMIR.ItemFloors f 
                                           JOIN IMTIAZ_SAMIR.FloorSections fs ON f.FloorId = fs.FloorId 
                                           WHERE f.SellerCatId = sc.SellerCatId) as TotalSeats
                                   FROM IMTIAZ_SAMIR.AppUsers u
                                   LEFT JOIN IMTIAZ_SAMIR.SellerCategory sc ON u.UserId = sc.SellerId
                                   LEFT JOIN IMTIAZ_SAMIR.Categories c ON sc.CategoryId = c.CategoryId
                                   WHERE u.RoleId = 3
                                   ORDER BY u.UserId DESC";

                    using (var cmd = new OracleCommand(sellerQuery, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.SellersList.Add(new SellerListItemDto
                                {
                                    UserId = reader.GetDecimal(0),
                                    FullName = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                                    Email = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                                    PhoneNumber = reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                                    Location = reader.IsDBNull(4) ? "N/A" : reader.GetString(4),
                                    ItemName = reader.GetString(5),
                                    CategoryName = reader.GetString(6),
                                    ServiceRating = reader.GetString(7),
                                    TotalFloors = Convert.ToInt32(reader[8]),
                                    TotalSeats = Convert.ToInt32(reader[9])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Load Sellers Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to load seller records: " + ex.Message;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSeller(SellersPageViewModel inputModel)
        {
            var s = inputModel.NewSeller;
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // 1. Create Seller User in AppUsers (RoleId = 3)
                    string insertUserQuery = @"INSERT INTO IMTIAZ_SAMIR.AppUsers 
                (UserId, FullName, Email, PasswordHash, RoleId, PhoneNumber, Address, Area, Thana, District, PostCode) 
                VALUES 
                (IMTIAZ_SAMIR.seq_users.NEXTVAL, :p_Name, :p_Email, :p_Pwd, 3, :p_Phone, :p_Addr, :p_Area, :p_Thana, :p_Dist, :p_Post)";

                    using (var cmd = new OracleCommand(insertUserQuery, conn))
                    {
                        cmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = s.FullName.Trim();
                        cmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = s.Email.Trim();
                        cmd.Parameters.Add("p_Pwd", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(s.Password) ? "123456" : s.Password;
                        cmd.Parameters.Add("p_Phone", OracleDbType.Varchar2).Value = s.PhoneNumber;
                        cmd.Parameters.Add("p_Addr", OracleDbType.Varchar2).Value = (object?)s.Address ?? DBNull.Value;
                        cmd.Parameters.Add("p_Area", OracleDbType.Varchar2).Value = (object?)s.Area ?? DBNull.Value;
                        cmd.Parameters.Add("p_Thana", OracleDbType.Varchar2).Value = (object?)s.Thana ?? DBNull.Value;
                        cmd.Parameters.Add("p_Dist", OracleDbType.Varchar2).Value = (object?)s.District ?? DBNull.Value;
                        cmd.Parameters.Add("p_Post", OracleDbType.Varchar2).Value = (object?)s.PostCode ?? DBNull.Value;

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Fetch Seller Id
                    decimal sellerId = 0;
                    using (var idCmd = new OracleCommand("SELECT UserId FROM IMTIAZ_SAMIR.AppUsers WHERE Email = :p_Email", conn))
                    {
                        idCmd.Parameters.Add("p_Email", OracleDbType.Varchar2).Value = s.Email.Trim();
                        var res = await idCmd.ExecuteScalarAsync();
                        if (res != null) sellerId = Convert.ToDecimal(res);
                    }

                    // 3. Insert into SellerCategory Table
                    decimal sellerCatId = 0;
                    if (sellerId > 0 && !string.IsNullOrWhiteSpace(s.ItemName))
                    {
                        string insertSellerCat = @"INSERT INTO IMTIAZ_SAMIR.SellerCategory 
                    (SellerCatId, CategoryId, SellerId, ItemName, ServiceRating, HasMultipleFloors) 
                    VALUES 
                    (IMTIAZ_SAMIR.seq_seller_category.NEXTVAL, :p_CatId, :p_SellerId, :p_ItemName, :p_Rating, :p_Multi)";

                        using (var scCmd = new OracleCommand(insertSellerCat, conn))
                        {
                            scCmd.Parameters.Add("p_CatId", OracleDbType.Decimal).Value = s.CategoryId;
                            scCmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;
                            scCmd.Parameters.Add("p_ItemName", OracleDbType.Varchar2).Value = s.ItemName.Trim();
                            scCmd.Parameters.Add("p_Rating", OracleDbType.Varchar2).Value = s.ServiceRating;
                            scCmd.Parameters.Add("p_Multi", OracleDbType.Decimal).Value = s.HasMultipleFloors ? 1 : 0;

                            await scCmd.ExecuteNonQueryAsync();
                        }

                        using (var scIdCmd = new OracleCommand("SELECT MAX(SellerCatId) FROM IMTIAZ_SAMIR.SellerCategory WHERE SellerId = :p_SellerId", conn))
                        {
                            scIdCmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;
                            var resSc = await scIdCmd.ExecuteScalarAsync();
                            if (resSc != null) sellerCatId = Convert.ToDecimal(resSc);
                        }
                    }

                    // 4. Save ItemFloors & FloorSections Dynamic Structure
                    if (sellerCatId > 0 && s.Floors != null && s.Floors.Any())
                    {
                        foreach (var fl in s.Floors)
                        {
                            decimal floorId = 0;
                            string insertFloor = @"INSERT INTO IMTIAZ_SAMIR.ItemFloors (FloorId, SellerCatId, FloorNumber, FloorName) 
                                           VALUES (IMTIAZ_SAMIR.seq_item_floors.NEXTVAL, :p_ScId, :p_Num, :p_Name)";

                            using (var flCmd = new OracleCommand(insertFloor, conn))
                            {
                                flCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;
                                flCmd.Parameters.Add("p_Num", OracleDbType.Decimal).Value = fl.FloorNumber;
                                flCmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(fl.FloorName) ? $"Floor {fl.FloorNumber}" : fl.FloorName;

                                await flCmd.ExecuteNonQueryAsync();
                            }

                            using (var flIdCmd = new OracleCommand("SELECT MAX(FloorId) FROM IMTIAZ_SAMIR.ItemFloors WHERE SellerCatId = :p_ScId", conn))
                            {
                                flIdCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;
                                var resFl = await flIdCmd.ExecuteScalarAsync();
                                if (resFl != null) floorId = Convert.ToDecimal(resFl);
                            }

                            if (floorId > 0 && fl.Sections != null)
                            {
                                foreach (var sec in fl.Sections)
                                {
                                    if (string.IsNullOrWhiteSpace(sec.SectionName)) continue;

                                    string insertSec = @"INSERT INTO IMTIAZ_SAMIR.FloorSections (SectionId, FloorId, SectionName, SeatCapacity) 
                                                 VALUES (IMTIAZ_SAMIR.seq_floor_sections.NEXTVAL, :p_FlId, :p_SecName, :p_Seats)";

                                    using (var secCmd = new OracleCommand(insertSec, conn))
                                    {
                                        secCmd.Parameters.Add("p_FlId", OracleDbType.Decimal).Value = floorId;
                                        secCmd.Parameters.Add("p_SecName", OracleDbType.Varchar2).Value = sec.SectionName;
                                        secCmd.Parameters.Add("p_Seats", OracleDbType.Decimal).Value = sec.SeatCapacity;

                                        await secCmd.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                        }
                    }

                    TempData["SuccessMessage"] = $"Seller '{s.FullName}' registered with dynamic floor structure successfully!";
                }
            }
            catch (OracleException ex) when (ex.Number == 1)
            {
                TempData["ErrorMessage"] = $"Email '{s.Email}' is already registered!";
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
        public async Task<IActionResult> DeleteSeller(decimal userId)
        {
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // Cascading delete on AppUsers will automatically clean up SellerCategory, ItemFloors, and FloorSections
                    using (var cmdUser = new OracleCommand("DELETE FROM IMTIAZ_SAMIR.AppUsers WHERE UserId = :p_Id", conn))
                    {
                        cmdUser.Parameters.Add("p_Id", OracleDbType.Decimal).Value = userId;
                        await cmdUser.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Seller and associated category structure deleted successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Delete Seller Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Failed to delete seller: " + ex.Message;
            }

            return RedirectToAction(nameof(Sellers));
        }

    }
}