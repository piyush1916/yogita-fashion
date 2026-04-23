using System.Text.Json.Serialization;

namespace YogitaFashionAPI.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public string Email { get; set; } = "";

        public string Password { get; set; } = "";

        public string Phone { get; set; } = "";

        public string City { get; set; } = "";

        public string Role { get; set; } = "Customer";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<Order> Orders { get; set; } = new List<Order>();

        [JsonIgnore]
        public ICollection<Address> Addresses { get; set; } = new List<Address>();

        [JsonIgnore]
        public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();

        [JsonIgnore]
        public ICollection<CouponUsageRecord> CouponUsageRecords { get; set; } = new List<CouponUsageRecord>();

        [JsonIgnore]
        public ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();

        [JsonIgnore]
        public ICollection<SupportRequest> SupportRequests { get; set; } = new List<SupportRequest>();

        [JsonIgnore]
        public ICollection<StockAlertSubscription> StockAlertSubscriptions { get; set; } = new List<StockAlertSubscription>();
    }
}
