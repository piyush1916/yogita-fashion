using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Controllers
{
    [ApiController]
    [Route("audit-logs")]
    [Authorize(Policy = "AdminOnly")]
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AuditLogsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetLogs(
            [FromQuery] int? userId = null,
            [FromQuery] string? role = null,
            [FromQuery] string? action = null,
            [FromQuery] string? module = null,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 500);

            IQueryable<AuditLogEntry> logs = _db.AuditLogEntries.AsNoTracking();

            if (userId.HasValue && userId.Value >= 0)
            {
                logs = logs.Where(item => item.UserId == userId.Value);
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                logs = logs.Where(item => item.Role == role.Trim());
            }

            if (!string.IsNullOrWhiteSpace(module))
            {
                logs = logs.Where(item => item.Module == module.Trim());
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                logs = logs.Where(item => item.Action == action.Trim());
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                logs = logs.Where(item => item.Status == status.Trim());
            }

            if (fromUtc.HasValue)
            {
                logs = logs.Where(item => item.TimestampUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                logs = logs.Where(item => item.TimestampUtc <= toUtc.Value);
            }

            var total = await logs.CountAsync();
            var items = await logs
                .OrderByDescending(item => item.TimestampUtc)
                .ThenByDescending(item => item.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                items
            });
        }
    }
}
