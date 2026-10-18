namespace EBS.Models
{
    public class SellerDashboardViewModel
    {
        public decimal SellerId { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;

        // Stats
        public int TotalFloors { get; set; }
        public int TotalCapacitySeats { get; set; }
        public int AllocatedCategorySeats { get; set; }
        public int RemainingUnallocatedSeats => TotalCapacitySeats - AllocatedCategorySeats;
        public int TotalCategoriesCount { get; set; }
        public int PendingBookingRequests { get; set; }

        // Lists for Overview
        public List<SeatCategoryDto> SeatCategories { get; set; } = new();
    }

    public class SeatCategoryDto
    {
        public decimal SeatCatId { get; set; }
        public string CategoryName { get; set; } = "Regular";
        public decimal PricePerSeat { get; set; }
        public int TotalSeats { get; set; }
    }
}