using System;
using System.Collections.Generic;

namespace EBS.Models
{
    public class CustomerVenueBookingViewModel
    {
        public decimal SellerCatId { get; set; }
        public List<VenueRequestItem> MyRequests { get; set; } = new List<VenueRequestItem>();
        public List<AvailableVenueItem> AvailableVenues { get; set; } = new List<AvailableVenueItem>();
    }

    public class VenueRequestItem
    {
        public decimal RequestId { get; set; }
        public string VenueName { get; set; }
        public int TotalSeats { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; }
        public DateTime RequestDate { get; set; }
        public string Notes { get; set; }
    }

    public class AvailableVenueItem
    {
        public decimal SellerCatId { get; set; }
        public string VenueName { get; set; }
        public int TotalCapacity { get; set; }
    }
}