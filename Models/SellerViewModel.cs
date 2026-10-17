using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EBS.Models
{
    public class CreateSellerViewModel
    {
        // Seller Personal / Account Info
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

        // Address Details
        public string? Address { get; set; }
        public string? Area { get; set; }
        public string? Thana { get; set; }
        public string? District { get; set; }
        public string? PostCode { get; set; }

        // Service / Business Details
        [Required(ErrorMessage = "Please select a Category")]
        public decimal CategoryId { get; set; }

        [Required(ErrorMessage = "Service/Space Name is required")]
        public string ItemName { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Capacity/Seat Quantity is required")]
        [Range(1, 100000, ErrorMessage = "Capacity must be at least 1")]
        public int Capacity { get; set; }

        [Required(ErrorMessage = "Service Rating is required")]
        public string ServiceRating { get; set; } = "3 Star"; // Default

        [Required(ErrorMessage = "Base Price is required")]
        public decimal BasePrice { get; set; }

        // Dropdown List Items
        public List<SelectListItem> CategoriesDropdown { get; set; } = new();
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
        public int Capacity { get; set; }
        public decimal BasePrice { get; set; }
    }

    public class SellersPageViewModel
    {
        public CreateSellerViewModel NewSeller { get; set; } = new();
        public List<SellerListItemDto> SellersList { get; set; } = new();
    }
}