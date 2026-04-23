using System.Security.Claims;

namespace YogitaFashionAPI.Services
{
    public interface IAuditLogService
    {
        Task WriteAsync(
            ClaimsPrincipal? actor,
            HttpContext? httpContext,
            string action,
            string module,
            string? entityId = null,
            string status = "Success",
            IDictionary<string, object?>? metadata = null,
            int? userIdOverride = null,
            string? roleOverride = null,
            CancellationToken cancellationToken = default);
    }
}
