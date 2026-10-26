using System;
using System.Collections.Generic;

namespace EBS.Models
{
    public class CommunityFeedViewModel
    {
        public List<PostItem> Posts { get; set; } = new List<PostItem>();
        public NewPostModel NewPost { get; set; } = new NewPostModel();
    }

    public class PostItem
    {
        public decimal PostId { get; set; }
        public decimal AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string AuthorRole { get; set; }
        public string PostType { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal? Budget { get; set; }
        public decimal? Price { get; set; }
        public int? Capacity { get; set; }
        public string Location { get; set; }
        public DateTime CreatedDate { get; set; }

        // সেলারের সিট ক্যাটাগরি লিস্ট (VIP, Regular, Business)
        public List<SellerCategoryDetail> Categories { get; set; } = new List<SellerCategoryDetail>();
    }

    public class SellerCategoryDetail
    {
        public string CategoryName { get; set; } // VIP, Regular, Business
        public int TotalSeats { get; set; }
        public decimal PricePerSeat { get; set; }
    }

    public class NewPostModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal? Budget { get; set; }
        public int? Capacity { get; set; }
        public string Location { get; set; }
    }
}