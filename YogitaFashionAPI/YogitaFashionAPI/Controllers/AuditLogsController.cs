using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [ApiController]
    [Route("audit-logs")]
    [Authorize(Policy = "AdminOnly")]
    public class AuditLogsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetLogs([FromQuery] string? entityType = null, [FromQuery] string? action = null)
        {
            var logs = AuditLogStore.GetAll().AsEnumerable();

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                logs = logs.Where(item => string.Equals(item.EntityType, entityType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                logs = logs.Where(item => string.Equals(item.Action, action, StringComparison.OrdinalIgnoreCase));
            }

            return Ok(logs.ToList());
        }
    }
}
