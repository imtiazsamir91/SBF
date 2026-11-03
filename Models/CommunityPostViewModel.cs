using System;

namespace EBS.Models
{
    public class CommunityPostViewModel
    {
        public decimal PostId { get; set; }
        public decimal AuthorId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorRole { get; set; } = string.Empty;
        public string PostType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? Budget { get; set; }
        public decimal? Price { get; set; }
        public int? Capacity { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }
}