using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using EBS.Models;

namespace EBS.Controllers
{
    public class SellerController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SellerController> _logger;

        public SellerController(IConfiguration configuration, ILogger<SellerController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private decimal GetLoggedInSellerId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 1;
        }

        // ==========================================
        // 1. SELLER DASHBOARD
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            decimal sellerId = GetLoggedInSellerId();
            var model = new SellerDashboardViewModel { SellerId = sellerId };
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    string infoQuery = @"SELECT u.FullName, sc.ItemName, c.CategoryName, sc.SellerCatId 
                                         FROM IMTIAZ_SAMIR.AppUsers u
                                         LEFT JOIN IMTIAZ_SAMIR.SellerCategory sc ON u.UserId = sc.SellerId
                                         LEFT JOIN IMTIAZ_SAMIR.Categories c ON sc.CategoryId = c.CategoryId
                                         WHERE u.UserId = :p_SellerId";

                    decimal sellerCatId = 0;
                    using (var cmd = new OracleCommand(infoQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                model.SellerName = reader.IsDBNull(0) ? "Seller" : reader.GetString(0);
                                model.ItemName = reader.IsDBNull(1) ? "No Venue Set" : reader.GetString(1);
                                model.CategoryName = reader.IsDBNull(2) ? "General" : reader.GetString(2);
                                sellerCatId = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                            }
                        }
                    }

                    using (var pkgCmd = new OracleCommand("IMTIAZ_SAMIR.BookingEnginePkg.get_seller_dashboard_stats", conn))
                    {
                        pkgCmd.CommandType = CommandType.StoredProcedure;
                        pkgCmd.BindByName = true;

                        pkgCmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;
                        pkgCmd.Parameters.Add("p_TotalFloors", OracleDbType.Decimal, ParameterDirection.Output);
                        pkgCmd.Parameters.Add("p_TotalSeats", OracleDbType.Decimal, ParameterDirection.Output);
                        pkgCmd.Parameters.Add("p_TotalCategories", OracleDbType.Decimal, ParameterDirection.Output);
                        pkgCmd.Parameters.Add("p_PendingRequests", OracleDbType.Decimal, ParameterDirection.Output);

                        await pkgCmd.ExecuteNonQueryAsync();

                        model.TotalFloors = Convert.ToInt32(((Oracle.ManagedDataAccess.Types.OracleDecimal)pkgCmd.Parameters["p_TotalFloors"].Value).Value);
                        model.TotalCapacitySeats = Convert.ToInt32(((Oracle.ManagedDataAccess.Types.OracleDecimal)pkgCmd.Parameters["p_TotalSeats"].Value).Value);
                        model.TotalCategoriesCount = Convert.ToInt32(((Oracle.ManagedDataAccess.Types.OracleDecimal)pkgCmd.Parameters["p_TotalCategories"].Value).Value);
                        model.PendingBookingRequests = Convert.ToInt32(((Oracle.ManagedDataAccess.Types.OracleDecimal)pkgCmd.Parameters["p_PendingRequests"].Value).Value);
                    }

                    if (sellerCatId > 0)
                    {
                        string catListQuery = @"SELECT SeatCatId, CategoryName, PricePerSeat, TotalSeats 
                                                FROM IMTIAZ_SAMIR.SeatCategories 
                                                WHERE SellerCatId = :p_ScId ORDER BY SeatCatId ASC";

                        using (var catCmd = new OracleCommand(catListQuery, conn))
                        {
                            catCmd.BindByName = true;
                            catCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = sellerCatId;

                            using (var catReader = await catCmd.ExecuteReaderAsync())
                            {
                                while (await catReader.ReadAsync())
                                {
                                    int seats = Convert.ToInt32(catReader.GetDecimal(3));
                                    model.AllocatedCategorySeats += seats;

                                    model.SeatCategories.Add(new SeatCategoryDto
                                    {
                                        SeatCatId = catReader.GetDecimal(0),
                                        CategoryName = catReader.GetString(1),
                                        PricePerSeat = catReader.GetDecimal(2),
                                        TotalSeats = seats
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Seller Dashboard Error: {Message}", ex.Message);
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ManageSeatCategories()
        {
            // ফিক্সড ১৫ বাদ দিয়ে ডাইনামিক সেশন আইডি কল করা হলো
            decimal sellerId = GetLoggedInSellerId();
            var model = new SeatCategoryManagementViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    // ১. সেলারের SellerCatId এবং AllowFullVenue সরাসরি বের করা
                    string catQuery = "SELECT SellerCatId, NVL(ALLOW_FULL_VENUE, 0) FROM IMTIAZ_SAMIR.SellerCategory WHERE SellerId = :p_SellerId";
                    using (var cmd = new OracleCommand(catQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                model.SellerCatId = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                                model.AllowFullVenue = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetDecimal(1));
                            }
                        }
                    }

                    if (model.SellerCatId > 0)
                    {
                        // ২. ওই SellerCatId এর আন্ডারে থাকা ফ্লোর সেকশনের মোট সিট ক্যাপাসিটি যোগ করা
                        string capQuery = @"SELECT NVL(SUM(fs.SeatCapacity), 0) 
                                   FROM IMTIAZ_SAMIR.ItemFloors f 
                                   JOIN IMTIAZ_SAMIR.FloorSections fs ON f.FloorId = fs.FloorId 
                                   WHERE f.SellerCatId = :p_SellerCatId";

                        using (var capCmd = new OracleCommand(capQuery, conn))
                        {
                            capCmd.BindByName = true;
                            capCmd.Parameters.Add("p_SellerCatId", OracleDbType.Decimal).Value = model.SellerCatId;
                            var result = await capCmd.ExecuteScalarAsync();
                            model.TotalCapacitySeats = result != null && result != DBNull.Value ? Convert.ToInt32(Convert.ToDecimal(result)) : 0;
                        }

                        // ৩. ইতিমধ্যে তৈরি করা ক্যাটাগরিগুলোর সিট ফেচ করা
                        string listQuery = @"SELECT SeatCatId, CategoryName, PricePerSeat, TotalSeats 
                                   FROM IMTIAZ_SAMIR.SeatCategories 
                                   WHERE SellerCatId = :p_ScId ORDER BY SeatCatId ASC";

                        using (var listCmd = new OracleCommand(listQuery, conn))
                        {
                            listCmd.BindByName = true;
                            listCmd.Parameters.Add("p_ScId", OracleDbType.Decimal).Value = model.SellerCatId;

                            using (var rdr = await listCmd.ExecuteReaderAsync())
                            {
                                while (await rdr.ReadAsync())
                                {
                                    int seats = Convert.ToInt32(rdr.GetDecimal(3));
                                    model.AllocatedSeatsCount += seats;

                                    model.ExistingCategories.Add(new SeatCategoryDto
                                    {
                                        SeatCatId = rdr.GetDecimal(0),
                                        CategoryName = rdr.GetString(1),
                                        PricePerSeat = rdr.GetDecimal(2),
                                        TotalSeats = seats
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Error: {Message}", ex.Message);
            }

            // ৪. রিমেইনিং সিট ক্যালকুলেশন
            model.RemainingSeats = model.TotalCapacitySeats - model.AllocatedSeatsCount;
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateVenueBookingRule(decimal sellerCatId, bool allowFullVenue)
        {
            if (sellerCatId <= 0)
            {
                TempData["ErrorMessage"] = "Error: Seller venue record not found.";
                return RedirectToAction(nameof(ManageSeatCategories));
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;
            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    string updateQuery = "UPDATE IMTIAZ_SAMIR.SellerCategory SET ALLOW_FULL_VENUE = :p_Allow WHERE SellerCatId = :p_Id";
                    using (var cmd = new OracleCommand(updateQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_Allow", OracleDbType.Decimal).Value = allowFullVenue ? 1 : 0;
                        cmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = sellerCatId;
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                TempData["SuccessMessage"] = "Venue booking rule updated successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Update Rule Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error updating rule: " + ex.Message;
            }
            return RedirectToAction(nameof(ManageSeatCategories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSeatCategory(decimal sellerCatId, string categoryName, decimal pricePerSeat, int totalSeats)
        {
            if (sellerCatId <= 0)
            {
                TempData["ErrorMessage"] = "Error: Seller venue record not found.";
                return RedirectToAction(nameof(ManageSeatCategories));
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // ১. ক্যাপাসিটি চেক করা
                            string checkCapQuery = @"SELECT NVL(SUM(fs.SeatCapacity), 0) 
                                             FROM IMTIAZ_SAMIR.ItemFloors f 
                                             JOIN IMTIAZ_SAMIR.FloorSections fs ON f.FloorId = fs.FloorId 
                                             WHERE f.SellerCatId = :p_SellerCatId";

                            decimal totalCapacity = 0;
                            using (var capCmd = new OracleCommand(checkCapQuery, conn))
                            {
                                capCmd.Transaction = transaction;
                                capCmd.BindByName = true;
                                capCmd.Parameters.Add("p_SellerCatId", OracleDbType.Decimal).Value = sellerCatId;
                                var res = await capCmd.ExecuteScalarAsync();
                                if (res != null && res != DBNull.Value) totalCapacity = Convert.ToDecimal(res);
                            }

                            string checkAllocQuery = @"SELECT NVL(SUM(TotalSeats), 0) FROM IMTIAZ_SAMIR.SeatCategories WHERE SellerCatId = :p_SellerCatId AND LOWER(CategoryName) != LOWER(:p_CatName)";
                            decimal allocatedSoFar = 0;
                            using (var allocCmd = new OracleCommand(checkAllocQuery, conn))
                            {
                                allocCmd.Transaction = transaction;
                                allocCmd.BindByName = true;
                                allocCmd.Parameters.Add("p_SellerCatId", OracleDbType.Decimal).Value = sellerCatId;
                                allocCmd.Parameters.Add("p_CatName", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(categoryName) ? "Regular" : categoryName.Trim();
                                var res = await allocCmd.ExecuteScalarAsync();
                                if (res != null && res != DBNull.Value) allocatedSoFar = Convert.ToDecimal(res);
                            }

                            if ((allocatedSoFar + totalSeats) > totalCapacity)
                            {
                                TempData["ErrorMessage"] = "Failed! Allocated seats exceed total capacity limit.";
                                return RedirectToAction(nameof(ManageSeatCategories));
                            }

                            // ২. ক্যাটাগরি আগে থেকেই আছে কি না চেক করা বা নতুন ইনসার্ট করা
                            decimal seatCatId = 0;
                            string checkCatQuery = "SELECT SeatCatId FROM IMTIAZ_SAMIR.SeatCategories WHERE SellerCatId = :p_SellerCatId AND LOWER(CategoryName) = LOWER(:p_CatName)";
                            using (var checkCmd = new OracleCommand(checkCatQuery, conn))
                            {
                                checkCmd.Transaction = transaction;
                                checkCmd.BindByName = true;
                                checkCmd.Parameters.Add("p_SellerCatId", OracleDbType.Decimal).Value = sellerCatId;
                                checkCmd.Parameters.Add("p_CatName", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(categoryName) ? "Regular" : categoryName.Trim();
                                var res = await checkCmd.ExecuteScalarAsync();
                                if (res != null && res != DBNull.Value) seatCatId = Convert.ToDecimal(res);
                            }

                            if (seatCatId > 0)
                            {
                                // আপডেট
                                string updateCat = "UPDATE IMTIAZ_SAMIR.SeatCategories SET PricePerSeat = :p_Price, TotalSeats = :p_Seats WHERE SeatCatId = :p_Id";
                                using (var upCmd = new OracleCommand(updateCat, conn))
                                {
                                    upCmd.Transaction = transaction;
                                    upCmd.BindByName = true;
                                    upCmd.Parameters.Add("p_Price", OracleDbType.Decimal).Value = pricePerSeat;
                                    upCmd.Parameters.Add("p_Seats", OracleDbType.Decimal).Value = totalSeats;
                                    upCmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = seatCatId;
                                    await upCmd.ExecuteNonQueryAsync();
                                }

                                // আগের সিটগুলো মুছে ফেলা
                                string delSeats = "DELETE FROM IMTIAZ_SAMIR.SEATS WHERE CLASSID = :p_Id";
                                using (var delCmd = new OracleCommand(delSeats, conn))
                                {
                                    delCmd.Transaction = transaction;
                                    delCmd.BindByName = true;
                                    delCmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = seatCatId;
                                    await delCmd.ExecuteNonQueryAsync();
                                }
                            }
                            else
                            {
                                // নতুন ইনসার্ট এবং সঠিক আইডি রিট্রিভ করা
                                string insertCat = @"INSERT INTO IMTIAZ_SAMIR.SeatCategories (SeatCatId, SellerCatId, CategoryName, PricePerSeat, TotalSeats) 
                                             VALUES (IMTIAZ_SAMIR.seq_seat_categories.NEXTVAL, :p_SellerCatId, :p_Name, :p_Price, :p_Seats) 
                                             RETURNING SeatCatId INTO :p_NewId";

                                using (var insCmd = new OracleCommand(insertCat, conn))
                                {
                                    insCmd.Transaction = transaction;
                                    insCmd.BindByName = true;
                                    insCmd.Parameters.Add("p_SellerCatId", OracleDbType.Decimal).Value = sellerCatId;
                                    insCmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(categoryName) ? "Regular" : categoryName.Trim();
                                    insCmd.Parameters.Add("p_Price", OracleDbType.Decimal).Value = pricePerSeat;
                                    insCmd.Parameters.Add("p_Seats", OracleDbType.Decimal).Value = totalSeats;

                                    var outId = new OracleParameter("p_NewId", OracleDbType.Decimal, ParameterDirection.Output);
                                    insCmd.Parameters.Add(outId);

                                    await insCmd.ExecuteNonQueryAsync();
                                    seatCatId = Convert.ToDecimal(outId.Value.ToString());
                                }
                            }

                            // ৩. ফ্লোর ও সেকশন নাম বের করা
                            string floorName = "1";
                            string sectionName = "A";
                            string floorSecQuery = @"SELECT f.FloorName, fs.SectionName 
                                             FROM IMTIAZ_SAMIR.ItemFloors f
                                             JOIN IMTIAZ_SAMIR.FloorSections fs ON f.FloorId = fs.FloorId
                                             WHERE f.SellerCatId = :p_SellerCatId AND ROWNUM = 1";

                            using (var fsCmd = new OracleCommand(floorSecQuery, conn))
                            {
                                fsCmd.Transaction = transaction;
                                fsCmd.BindByName = true;
                                fsCmd.Parameters.Add("p_SellerCatId", OracleDbType.Decimal).Value = sellerCatId;
                                using (var reader = await fsCmd.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        floorName = reader.IsDBNull(0) ? "1" : reader.GetString(0);
                                        sectionName = reader.IsDBNull(1) ? "A" : reader.GetString(1);
                                    }
                                }
                            }

                            string cleanSection = string.IsNullOrWhiteSpace(sectionName) ? "A" : sectionName.Trim().Substring(0, 1).ToUpper();
                            var match = System.Text.RegularExpressions.Regex.Match(floorName ?? "", @"\d+");
                            string cleanFloor = match.Success ? match.Value : "1";

                            // ৪. সিট জেনারেট করে SEATS টেবিলে ইনসার্ট করা
                            int existingSeatsCount = 0;
                            string countQuery = "SELECT COUNT(*) FROM IMTIAZ_SAMIR.SEATS WHERE ITEMID = :p_ItemId";
                            using (var countCmd = new OracleCommand(countQuery, conn))
                            {
                                countCmd.Transaction = transaction;
                                countCmd.BindByName = true;
                                countCmd.Parameters.Add("p_ItemId", OracleDbType.Decimal).Value = sellerCatId;
                                var cRes = await countCmd.ExecuteScalarAsync();
                                if (cRes != null && cRes != DBNull.Value) existingSeatsCount = Convert.ToInt32(cRes);
                            }

                            string insertSeatQuery = @"INSERT INTO IMTIAZ_SAMIR.SEATS (ITEMID, CLASSID, SEATNUMBER, ISAVAILABLE) 
                                               VALUES (:p_ItemId, :p_ClassId, :p_SeatNum, 1)";

                            for (int i = 1; i <= totalSeats; i++)
                            {
                                string seatNumber = $"{cleanSection}{cleanFloor}{(existingSeatsCount + i).ToString("D4")}";

                                using (var seatCmd = new OracleCommand(insertSeatQuery, conn))
                                {
                                    seatCmd.Transaction = transaction;
                                    seatCmd.BindByName = true;
                                    seatCmd.Parameters.Add("p_ItemId", OracleDbType.Decimal).Value = sellerCatId;
                                    seatCmd.Parameters.Add("p_ClassId", OracleDbType.Decimal).Value = seatCatId;
                                    seatCmd.Parameters.Add("p_SeatNum", OracleDbType.Varchar2).Value = seatNumber;
                                    await seatCmd.ExecuteNonQueryAsync();
                                }
                            }

                            await transaction.CommitAsync();
                            TempData["SuccessMessage"] = $"Category saved and {totalSeats} seats generated successfully!";
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error: " + ex.Message;
            }

            return RedirectToAction(nameof(ManageSeatCategories));
        }
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> BookingRequests()
        {
            decimal sellerId = GetLoggedInSellerId();
            var model = new BookingRequestsViewModel();
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    string query = @"SELECT DISTINCT b.BookingId, u.FullName, u.Email, u.PhoneNumber, 
                                            b.TravelOrEventDate, b.TotalAmount, b.Status, b.LockExpiresAt
                                     FROM IMTIAZ_SAMIR.Bookings b
                                     JOIN IMTIAZ_SAMIR.AppUsers u ON b.UserId = u.UserId
                                     JOIN IMTIAZ_SAMIR.SellerCategory sc ON b.ItemId = sc.SellerCatId
                                     WHERE sc.SellerId = :p_SellerId
                                     ORDER BY b.BookingId DESC";

                    using (var cmd = new OracleCommand(query, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_SellerId", OracleDbType.Decimal).Value = sellerId;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.Requests.Add(new BookingRequestDto
                                {
                                    BookingId = reader.GetDecimal(0),
                                    CustomerName = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                                    CustomerEmail = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                                    CustomerPhone = reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                                    TravelOrEventDate = reader.GetDateTime(4),
                                    TotalAmount = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                                    Status = reader.GetString(6),
                                    LockExpiresAt = reader.IsDBNull(7) ? DateTime.Now : reader.GetDateTime(7)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Get Booking Requests Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error: " + ex.Message;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessRequest(decimal bookingId, string actionType, string? sellerNotes)
        {
            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new OracleCommand("IMTIAZ_SAMIR.BookingEnginePkg.ProcessBookingRequest", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.BindByName = true;

                        cmd.Parameters.Add("p_BookingId", OracleDbType.Decimal).Value = bookingId;
                        cmd.Parameters.Add("p_Action", OracleDbType.Varchar2).Value = actionType.ToUpper();
                        cmd.Parameters.Add("p_SellerNotes", OracleDbType.Varchar2).Value = (object?)sellerNotes ?? DBNull.Value;

                        var statusParam = new OracleParameter("p_Status", OracleDbType.Varchar2, 50);
                        statusParam.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(statusParam);

                        await cmd.ExecuteNonQueryAsync();

                        string resultStatus = statusParam.Value.ToString() ?? "";

                        if (resultStatus == "ACCEPTED")
                        {
                            TempData["SuccessMessage"] = $"Booking #{bookingId} has been Accepted & Confirmed!";
                        }
                        else if (resultStatus == "REJECTED")
                        {
                            TempData["SuccessMessage"] = $"Booking #{bookingId} has been Rejected/Cancelled.";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Failed to process booking request.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Process Request Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error: " + ex.Message;
            }

            return RedirectToAction(nameof(BookingRequests));
        }
        // ==========================================
        // UPDATE SEAT CATEGORY
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSeatCategory(decimal seatCatId, string categoryName, decimal pricePerSeat, int totalSeats)
        {
            if (seatCatId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Seat Category selected for update.";
                return RedirectToAction(nameof(ManageSeatCategories));
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        // ১. সবার আগে এই SeatCatId এর বিপরীতে সঠিক SellerCatId (Item ID) বের করে নেওয়া
                        decimal sellerCatId = 0;
                        string getSellerCatQuery = "SELECT SellerCatId FROM IMTIAZ_SAMIR.SeatCategories WHERE SeatCatId = :p_Id";
                        using (var getCmd = new OracleCommand(getSellerCatQuery, conn))
                        {
                            getCmd.Transaction = transaction;
                            getCmd.BindByName = true;
                            getCmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = seatCatId;
                            var res = await getCmd.ExecuteScalarAsync();
                            if (res != null && res != DBNull.Value)
                            {
                                sellerCatId = Convert.ToDecimal(res);
                            }
                        }

                        if (sellerCatId <= 0)
                        {
                            TempData["ErrorMessage"] = "Error: Parent Seller Category not found.";
                            return RedirectToAction(nameof(ManageSeatCategories));
                        }

                        // ২. ক্যাটাগরি টেবিল আপডেট করা
                        string updateCatQuery = @"UPDATE IMTIAZ_SAMIR.SeatCategories 
                                       SET CategoryName = :p_Name, 
                                           PricePerSeat = :p_Price, 
                                           TotalSeats = :p_Seats 
                                       WHERE SeatCatId = :p_Id";

                        using (var cmd = new OracleCommand(updateCatQuery, conn))
                        {
                            cmd.Transaction = transaction;
                            cmd.BindByName = true;
                            cmd.Parameters.Add("p_Name", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(categoryName) ? "Regular" : categoryName.Trim();
                            cmd.Parameters.Add("p_Price", OracleDbType.Decimal).Value = pricePerSeat;
                            cmd.Parameters.Add("p_Seats", OracleDbType.Decimal).Value = totalSeats;
                            cmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = seatCatId;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // ৩. এই ক্যাটাগরির আগের জেনারেট করা সিটগুলো SEATS টেবিল থেকে মুছে ফেলা
                        string deleteOldSeats = "DELETE FROM IMTIAZ_SAMIR.SEATS WHERE CLASSID = :p_ClassId";
                        using (var delCmd = new OracleCommand(deleteOldSeats, conn))
                        {
                            delCmd.Transaction = transaction;
                            delCmd.BindByName = true;
                            delCmd.Parameters.Add("p_ClassId", OracleDbType.Decimal).Value = seatCatId;
                            await delCmd.ExecuteNonQueryAsync();
                        }

                        // ৪. ফ্লোর ও সেকশন নাম বের করা
                        string floorName = "1";
                        string sectionName = "A";
                        string floorSecQuery = @"SELECT f.FloorName, fs.SectionName 
                                         FROM IMTIAZ_SAMIR.ItemFloors f
                                         JOIN IMTIAZ_SAMIR.FloorSections fs ON f.FloorId = fs.FloorId
                                         WHERE f.SellerCatId = :p_SellerCatId AND ROWNUM = 1";

                        using (var fsCmd = new OracleCommand(floorSecQuery, conn))
                        {
                            fsCmd.Transaction = transaction;
                            fsCmd.BindByName = true;
                            fsCmd.Parameters.Add("p_SellerCatId", OracleDbType.Decimal).Value = sellerCatId;
                            using (var fsReader = await fsCmd.ExecuteReaderAsync())
                            {
                                if (await fsReader.ReadAsync())
                                {
                                    floorName = fsReader.IsDBNull(0) ? "1" : fsReader.GetString(0);
                                    sectionName = fsReader.IsDBNull(1) ? "A" : fsReader.GetString(1);
                                }
                            }
                        }

                        string cleanSection = string.IsNullOrWhiteSpace(sectionName) ? "A" : sectionName.Trim().Substring(0, 1).ToUpper();
                        var match = System.Text.RegularExpressions.Regex.Match(floorName ?? "", @"\d+");
                        string cleanFloor = match.Success ? match.Value : "1";

                        // ৫. নতুন সিট সংখ্যা অনুযায়ী আবার সিট কোড রি-জেনারেট করে SEATS টেবিলে ইনসার্ট করা
                        string insertSeatQuery = @"INSERT INTO IMTIAZ_SAMIR.SEATS (ITEMID, CLASSID, SEATNUMBER, ISAVAILABLE) 
                                           VALUES (:p_ItemId, :p_ClassId, :p_SeatNum, 1)";

                        for (int i = 1; i <= totalSeats; i++)
                        {
                            string seatNumber = $"{cleanSection}{cleanFloor}{i.ToString("D4")}";

                            using (var seatCmd = new OracleCommand(insertSeatQuery, conn))
                            {
                                seatCmd.Transaction = transaction;
                                seatCmd.BindByName = true;
                                // এখানে এখন সঠিক sellerCatId (যেমন: 1 বা 6) পাস হবে, ফলে FK_SEATS_ITEMS এরর আর আসবে না
                                seatCmd.Parameters.Add("p_ItemId", OracleDbType.Decimal).Value = sellerCatId;
                                seatCmd.Parameters.Add("p_ClassId", OracleDbType.Decimal).Value = seatCatId;
                                seatCmd.Parameters.Add("p_SeatNum", OracleDbType.Varchar2).Value = seatNumber;
                                await seatCmd.ExecuteNonQueryAsync();
                            }
                        }

                        await transaction.CommitAsync();
                    }

                    TempData["SuccessMessage"] = "Seat category and seat numbers updated successfully!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Update Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error updating category: " + ex.Message;
            }

            return RedirectToAction(nameof(ManageSeatCategories));
        }
        // ==========================================
        // DELETE SEAT CATEGORY
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSeatCategory(decimal seatCatId)
        {
            if (seatCatId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Seat Category selected for deletion.";
                return RedirectToAction(nameof(ManageSeatCategories));
            }

            string connString = _configuration.GetConnectionString("OracleDbContext")!;

            try
            {
                using (var conn = new OracleConnection(connString))
                {
                    await conn.OpenAsync();

                    string deleteQuery = "DELETE FROM IMTIAZ_SAMIR.SeatCategories WHERE SeatCatId = :p_Id";

                    using (var cmd = new OracleCommand(deleteQuery, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_Id", OracleDbType.Decimal).Value = seatCatId;

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Seat category deleted successfully!";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Failed to delete category.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Delete Seat Category Error: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error deleting category: " + ex.Message;
            }

            return RedirectToAction(nameof(ManageSeatCategories));
        }
    }
}