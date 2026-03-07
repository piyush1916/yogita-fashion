namespace YogitaFashionAPI.Models
{
    public class AuditLogEntry
    {
        public int Id { get; set; }
        public int ActorId { get; set; }
        public string ActorEmail { get; set; } = "";
        public string ActorRole { get; set; } = "";
        public string Action { get; set; } = "";
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Details { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
