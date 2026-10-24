namespace EBS.Models
{
    public class SeatCategoryManagementViewModel
    {
        public decimal SellerCatId { get; set; }
        public int TotalCapacitySeats { get; set; }
        public int AllocatedSeatsCount { get; set; }
        public int RemainingSeats { get; set; }
        public int AllowFullVenue { get; set; }

     
        public string StartSeatNumber { get; set; } = ""; 
        public string EndSeatNumber { get; set; } = "";   

        public List<SeatCategoryDto> ExistingCategories { get; set; } = new();
    }
}