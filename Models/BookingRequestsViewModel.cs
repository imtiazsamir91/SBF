namespace EBS.Models
{
    public class BookingRequestsViewModel
    {
        public List<BookingRequestDto> Requests { get; set; } = new();
    }

    public class BookingRequestDto
    {
        public decimal BookingId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime TravelOrEventDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LockExpiresAt { get; set; }
    }
}