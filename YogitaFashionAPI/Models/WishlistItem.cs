using System.Text.Json.Serialization;

namespace YogitaFashionAPI.Models
{
    public class WishlistItem
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int ProductId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public User? User { get; set; }

        [JsonIgnore]
        public Product? Product { get; set; }
    }
}
