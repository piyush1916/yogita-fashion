using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace YogitaFashionAPI.Models
{
    public class SupportRequest
    {
        public int Id { get; set; }

        public int? UserId { get; set; }

        public string Name { get; set; } = "";

        public string Contact { get; set; } = "";

        public string Subject { get; set; } = "";

        [NotMapped]
        public string OrderId
        {
            get => OrderReference;
            set => OrderReference = value ?? "";
        }

        public int? OrderDbId { get; set; }

        public string OrderReference { get; set; } = "";

        public string Message { get; set; } = "";

        public string Email { get; set; } = "";

        public string Phone { get; set; } = "";

        public string Status { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public User? User { get; set; }

        [JsonIgnore]
        public Order? Order { get; set; }
    }
}
