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
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorRole { get; set; } = string.Empty; // CUSTOMER or SELLER
        public string PostType { get; set; } = string.Empty;   // USER_REQUIREMENT or SELLER_VENUE
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? Budget { get; set; }
        public decimal? Price { get; set; }
        public int? Capacity { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }

        // সেলারের সিট ক্যাটাগরি লিস্ট (VIP, Regular, Business)
        public List<SellerCategoryDetail> Categories { get; set; } = new List<SellerCategoryDetail>();

        // 🔑 নতুন যুক্ত করা হলো: কাস্টমার পোস্টে সেলারদের পাঠানো প্রোপোজাল বা রেসপন্স লিস্ট
        public List<PostResponseItem> Responses { get; set; } = new List<PostResponseItem>();
    }

    // 🔑 নতুন যুক্ত করা হলো: সেলার রেসপন্স বা প্রোপোজালের মডেল
    public class PostResponseItem
    {
        public decimal ResponseId { get; set; }
        public decimal PostId { get; set; }
        public decimal SellerId { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public string ProposalMessage { get; set; } = string.Empty;
        public decimal OfferPrice { get; set; }
        public string Status { get; set; } = "PENDING";
        public DateTime CreatedDate { get; set; }
    }

    public class SellerCategoryDetail
    {
        public string CategoryName { get; set; } = string.Empty; // VIP, Regular, Business
        public int TotalSeats { get; set; }
        public decimal PricePerSeat { get; set; }
    }

    public class NewPostModel
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? Budget { get; set; }
        public int? Capacity { get; set; }
        public string Location { get; set; } = string.Empty;
    }
}