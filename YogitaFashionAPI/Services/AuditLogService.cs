using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Services
{
    public class AuditLogService : IAuditLogService
    {
        private static readonly SemaphoreSlim WriteLock = new(1, 1);
        private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "password",
            "confirmPassword",
            "oldPassword",
            "newPassword",
            "token",
            "jwt",
            "authorization",
            "secret",
            "apikey",
            "apiKey",
            "key",
            "card",
            "cardNumber",
            "cvv",
            "otp"
        };

        private readonly AppDbContext _db;
        private readonly ILogger<AuditLogService> _logger;
        private readonly byte[] _integrityKey;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false
        };

        public AuditLogService(AppDbContext db, IConfiguration configuration, ILogger<AuditLogService> logger)
        {
            _db = db;
            _logger = logger;
            var configuredIntegrityKey = (configuration["Audit:IntegrityKey"] ?? string.Empty).Trim();
            var fallbackJwtKey = (configuration["Jwt:Key"] ?? string.Empty).Trim();
            var effectiveKey = !string.IsNullOrWhiteSpace(configuredIntegrityKey)
                ? configuredIntegrityKey
                : (!string.IsNullOrWhiteSpace(fallbackJwtKey) ? fallbackJwtKey : "local-dev-audit-integrity-key");
            _integrityKey = Encoding.UTF8.GetBytes(effectiveKey);
        }

        public async Task WriteAsync(
            ClaimsPrincipal? actor,
            HttpContext? httpContext,
            string action,
            string module,
            string? entityId = null,
            string status = "Success",
            IDictionary<string, object?>? metadata = null,
            int? userIdOverride = null,
            string? roleOverride = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(module))
            {
                return;
            }

            var userId = userIdOverride ?? ResolveUserId(actor);
            var role = string.IsNullOrWhiteSpace(roleOverride)
                ? ResolveRole(actor)
                : roleOverride.Trim();
            var ipAddress = ResolveIpAddress(httpContext);
            var safeMetadata = SanitizeMetadata(metadata ?? new Dictionary<string, object?>());
            var metadataJson = JsonSerializer.Serialize(safeMetadata, _jsonOptions);

            var entry = new AuditLogEntry
            {
                UserId = userId > 0 ? userId : null,
                Role = Limit(role, 40),
                Action = Limit(action.Trim(), 120),
                Module = Limit(module.Trim(), 120),
                EntityId = Limit((entityId ?? string.Empty).Trim(), 120),
                TimestampUtc = DateTime.UtcNow,
                IpAddress = Limit(ipAddress, 64),
                Status = Limit(string.IsNullOrWhiteSpace(status) ? "Success" : status.Trim(), 20),
                MetadataJson = Limit(metadataJson, 16000),
                RequestPath = Limit(httpContext?.Request.Path.Value ?? string.Empty, 255),
                HttpMethod = Limit(httpContext?.Request.Method ?? string.Empty, 10)
            };

            await WriteLock.WaitAsync(cancellationToken);
            try
            {
                entry.PreviousHash = await _db.AuditLogEntries
                    .AsNoTracking()
                    .OrderByDescending(item => item.Id)
                    .Select(item => item.EntryHash)
                    .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

                entry.EntryHash = ComputeHash(entry);
                _db.AuditLogEntries.Add(entry);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to persist audit log for action {Action} and module {Module}.", action, module);
            }
            finally
            {
                WriteLock.Release();
            }
        }

        private string ComputeHash(AuditLogEntry entry)
        {
            var payload =
                $"{entry.UserId}|{entry.Role}|{entry.Action}|{entry.Module}|{entry.EntityId}|{entry.TimestampUtc:O}|{entry.IpAddress}|{entry.Status}|{entry.MetadataJson}|{entry.RequestPath}|{entry.HttpMethod}|{entry.PreviousHash}";

            using var hmac = new HMACSHA256(_integrityKey);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash);
        }

        private static int ResolveUserId(ClaimsPrincipal? actor)
        {
            var idClaim =
                actor?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                actor?.FindFirstValue("sub") ??
                string.Empty;
            return int.TryParse(idClaim, out var parsedId) ? parsedId : 0;
        }

        private static string ResolveRole(ClaimsPrincipal? actor)
        {
            return (actor?.FindFirstValue(ClaimTypes.Role) ??
                    actor?.FindFirstValue("role") ??
                    "Anonymous").Trim();
        }

        private static string ResolveIpAddress(HttpContext? context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                var first = forwardedFor.ToString()
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first;
                }
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        }

        private static Dictionary<string, object?> SanitizeMetadata(IDictionary<string, object?> metadata)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in metadata)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                if (SensitiveKeys.Contains(pair.Key))
                {
                    result[pair.Key] = "[REDACTED]";
                    continue;
                }

                result[pair.Key] = SanitizeValue(pair.Value);
            }

            return result;
        }

        private static object? SanitizeValue(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string text)
            {
                return Limit(text, 500);
            }

            if (value is IDictionary<string, object?> dict)
            {
                return SanitizeMetadata(dict);
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                var safeItems = new List<object?>();
                foreach (var item in enumerable)
                {
                    if (safeItems.Count >= 25)
                    {
                        break;
                    }

                    safeItems.Add(SanitizeValue(item));
                }

                return safeItems;
            }

            return value;
        }

        private static string Limit(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
