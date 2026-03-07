using System.Security.Claims;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Services
{
    public static class AuditLogStore
    {
        private static readonly object Sync = new();
        private static readonly List<AuditLogEntry> Entries = new();

        public static IReadOnlyList<AuditLogEntry> GetAll()
        {
            lock (Sync)
            {
                return Entries.OrderByDescending(item => item.CreatedAt).ToList();
            }
        }

        public static void Add(ClaimsPrincipal actor, string action, string entityType, string entityId, string details)
        {
            var actorIdClaim = actor.FindFirstValue(ClaimTypes.NameIdentifier);
            var actorEmail = actor.FindFirstValue(ClaimTypes.Email) ?? "";
            var actorRole = actor.FindFirstValue(ClaimTypes.Role) ?? "";
            var actorId = int.TryParse(actorIdClaim, out var parsedId) ? parsedId : 0;

            lock (Sync)
            {
                var entry = new AuditLogEntry
                {
                    Id = Entries.Count == 0 ? 1 : Entries.Max(item => item.Id) + 1,
                    ActorId = actorId,
                    ActorEmail = actorEmail,
                    ActorRole = actorRole,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Details = details,
                    CreatedAt = DateTime.UtcNow,
                };
                Entries.Add(entry);
            }
        }
    }
}
