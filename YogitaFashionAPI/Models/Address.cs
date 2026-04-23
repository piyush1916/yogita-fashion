using System.Text.Json.Serialization;

namespace YogitaFashionAPI.Models
{
    public class Address
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string FullName { get; set; } = "";

        public string Phone { get; set; } = "";

        public string City { get; set; } = "";

        public string State { get; set; } = "";

        public string Pincode { get; set; } = "";

        public string Country { get; set; } = "India";

        public string Street { get; set; } = "";

        public string Line2 { get; set; } = "";

        public string Landmark { get; set; } = "";

        public string AddressType { get; set; } = "Home";

        public bool IsDefault { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public User? User { get; set; }
    }
}
