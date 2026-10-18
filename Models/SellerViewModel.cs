using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EBS.Models
{
    public class CreateSellerViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required")]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Address { get; set; }
        public string? Area { get; set; }
        public string? Thana { get; set; }
        public string? District { get; set; }
        public string? PostCode { get; set; }

        [Required(ErrorMessage = "Please select a Category")]
        public decimal CategoryId { get; set; }

        [Required(ErrorMessage = "Service/Space Name is required")]
        public string ItemName { get; set; } = string.Empty;

        public string? Description { get; set; }
        public int Capacity { get; set; }
        public string ServiceRating { get; set; } = "3 Star";
        public decimal BasePrice { get; set; }
        public string Status { get; set; } = "Active";

        public bool HasMultipleFloors { get; set; }
        public List<FloorInputDto> Floors { get; set; } = new();
        public List<SelectListItem> CategoriesDropdown { get; set; } = new();
    }

    public class FloorInputDto
    {
        public int FloorNumber { get; set; }
        public string FloorName { get; set; } = string.Empty;
        public List<SectionInputDto> Sections { get; set; } = new();
    }

    public class SectionInputDto
    {
        public string SectionName { get; set; } = string.Empty;
        public int SeatCapacity { get; set; }
    }

    public class SellerListItemDto
    {
        public decimal UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string ServiceRating { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public string Status { get; set; } = "Active";
        public int TotalFloors { get; set; }
        public int TotalSeats { get; set; }
    }

    public class SellersPageViewModel
    {
        public CreateSellerViewModel NewSeller { get; set; } = new();
        public List<SellerListItemDto> SellersList { get; set; } = new();
    }
}