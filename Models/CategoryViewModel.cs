using System.ComponentModel.DataAnnotations;

namespace EBS.Models
{
    public class CategoryViewModel
    {
        public decimal CategoryId { get; set; }

        [Required(ErrorMessage = "Category Name is required")]
        [StringLength(50, ErrorMessage = "Category Name cannot exceed 50 characters")]
        public string CategoryName { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters")]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class CategoriesPageViewModel
    {
        public CategoryViewModel NewCategory { get; set; } = new CategoryViewModel();
        public List<CategoryViewModel> CategoriesList { get; set; } = new List<CategoryViewModel>();
    }
}