using System.ComponentModel.DataAnnotations;

namespace EBS.Models
{
    public class BookingRequestViewModel
    {
        [Required]
        public decimal ItemId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime TravelDate { get; set; }

        // Comma-separated Seat IDs (e.g., "101,102,103")
        public string SeatIdList { get; set; } = string.Empty;

        [Range(1, 100)]
        public int SeatCount { get; set; }

        // 1 = Full Venue requested, 0 = Normal Seat Booking
        public int RequestFullVenue { get; set; } = 0;
    }

    public class BookingResponseViewModel
    {
        public bool IsSuccess { get; set; }
        public decimal BookingId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}