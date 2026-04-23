using System.Text.Json.Serialization;

namespace YogitaFashionAPI.Models
{
    public class AuditLogEntry
    {
        public long Id { get; set; }
        public int? UserId { get; set; }
        public string Role { get; set; } = "";
        public string Action { get; set; } = "";
        public string Module { get; set; } = "";
        public string EntityId { get; set; } = "";
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string IpAddress { get; set; } = "";
        public string Status { get; set; } = "Success";
        public string MetadataJson { get; set; } = "{}";
        public string RequestPath { get; set; } = "";
        public string HttpMethod { get; set; } = "";
        public string PreviousHash { get; set; } = "";
        public string EntryHash { get; set; } = "";

        [JsonIgnore]
        public User? User { get; set; }
    }
}
