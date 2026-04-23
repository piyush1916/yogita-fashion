using System.Text.Json.Serialization;

namespace YogitaFashionAPI.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal Mrp { get; set; }
        public string Category { get; set; } = "";
        public string Brand { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Description { get; set; } = "";
        public string Size { get; set; } = "";
        public string Color { get; set; } = "";
        public List<string> Sizes { get; set; } = new();
        public List<string> Colors { get; set; } = new();
        public int Stock { get; set; }
        public bool FeaturedProduct { get; set; }
        public bool IsBestSeller { get; set; }
        public ProductDetails Details { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<OrderLineItem> OrderLineItems { get; set; } = new List<OrderLineItem>();

        [JsonIgnore]
        public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();

        [JsonIgnore]
        public ICollection<StockAlertSubscription> StockAlertSubscriptions { get; set; } = new List<StockAlertSubscription>();

        [JsonIgnore]
        public ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();
    }

    public class ProductDetails
    {
        public string Material { get; set; } = "";
        public string Fit { get; set; } = "";
        public string Style { get; set; } = "";
        public string Sleeve { get; set; } = "";
        public string WashCare { get; set; } = "";
    }
}
