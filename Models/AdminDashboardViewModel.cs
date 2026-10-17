namespace EBS.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalSellers { get; set; }
        public int TotalCategories { get; set; }
        public int TotalBookings { get; set; }
        public List<AdminBookingDto> RecentBookings { get; set; } = new();
    }

    public class AdminBookingDto
    {
        public decimal BookingId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public DateTime TravelDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }
}
namespace MultiVendorBooking.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalSellers { get; set; }
        public int TotalCategories { get; set; }
        public int TotalBookings { get; set; }
        public List<AdminBookingDto> RecentBookings { get; set; } = new();
    }

    public class AdminBookingDto
    {
        public decimal BookingId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public DateTime TravelDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }
}