using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _db;
        private readonly IAuditLogService _auditLogService;

        public AuthController(IConfiguration configuration, AppDbContext db, IAuditLogService auditLogService)
        {
            _configuration = configuration;
            _db = db;
            _auditLogService = auditLogService;
        }

        [HttpGet("users")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetUsers()
        {
            var items = await _db.Users
                .OrderByDescending(user => user.CreatedAt)
                .Select(user => ToPublicUser(user))
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] User input)
        {
            if (input == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "Signup", "Auth", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest("Registration payload is required.");
            }

            var email = (input.Email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(input.Password))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "Signup", "Auth", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["email"] = email,
                    ["reason"] = "missing_email_or_password"
                });
                return BadRequest("Email and password are required.");
            }

            if (input.Password.Trim().Length < 6)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "Signup", "Auth", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["email"] = email,
                    ["reason"] = "password_too_short"
                });
                return BadRequest("Password must be at least 6 characters.");
            }

            var alreadyExists = await _db.Users.AnyAsync(existing => (existing.Email ?? "").ToLower() == email);
            if (alreadyExists)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "Signup", "Auth", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["email"] = email,
                    ["reason"] = "duplicate_email"
                });
                return Conflict("Email already registered.");
            }

            var requesterRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var requestedRole = (input.Role ?? "").Trim();
            var role = string.Equals(requesterRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
                       !string.IsNullOrWhiteSpace(requestedRole)
                ? requestedRole
                : "Customer";

            var user = new User
            {
                Name = (input.Name ?? "").Trim(),
                Email = email,
                Password = PasswordService.HashPassword(input.Password.Trim()),
                Phone = (input.Phone ?? "").Trim(),
                City = (input.City ?? "").Trim(),
                Role = role,
                CreatedAt = input.CreatedAt == default ? DateTime.UtcNow : input.CreatedAt
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync(User, HttpContext, "Signup", "Auth", user.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["email"] = email,
                ["role"] = user.Role
            }, user.Id, user.Role);

            var token = JwtTokenService.GenerateToken(user, _configuration);
            return Ok(new
            {
                token,
                user = ToPublicUser(user)
            });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] User loginUser)
        {
            if (loginUser == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "Login", "Auth", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest("Request body is required.");
            }

            var email = (loginUser.Email ?? string.Empty).Trim().ToLowerInvariant();
            var password = loginUser.Password ?? string.Empty;

            var user = await _db.Users.FirstOrDefaultAsync(existing =>
                (existing.Email ?? "").ToLower() == email);

            if (user == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "Login", "Auth", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["email"] = email,
                    ["reason"] = "user_not_found"
                });
                return Unauthorized("Invalid email or password.");
            }

            var storedPassword = user.Password ?? string.Empty;
            var isLegacyPassword = !PasswordService.IsPasswordHashed(storedPassword);
            var isValidPassword = isLegacyPassword
                ? string.Equals(storedPassword, password, StringComparison.Ordinal)
                : PasswordService.VerifyPassword(password, storedPassword);

            if (!isValidPassword)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "Login", "Auth", user.Id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["email"] = email,
                    ["reason"] = "invalid_password"
                }, user.Id, user.Role);
                return Unauthorized("Invalid email or password.");
            }

            if (isLegacyPassword)
            {
                user.Password = PasswordService.HashPassword(password);
                await _db.SaveChangesAsync();
            }

            await _auditLogService.WriteAsync(User, HttpContext, "Login", "Auth", user.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["email"] = email
            }, user.Id, user.Role);
            var token = JwtTokenService.GenerateToken(user, _configuration);
            return Ok(new
            {
                token,
                user = ToPublicUser(user)
            });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var user = await _db.Users.FirstOrDefaultAsync(existing => existing.Id == currentUserId.Value);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            return Ok(ToPublicUser(user));
        }

        [HttpGet("profile/{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetProfile(int id)
        {
            if (!CanAccessUser(id))
            {
                return Forbid();
            }

            var user = await _db.Users.FirstOrDefaultAsync(existing => existing.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(ToPublicUser(user));
        }

        [HttpPut("profile/{id:int}")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile(int id, [FromBody] User payload)
        {
            if (payload == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ProfileUpdate", "UserProfile", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest("Profile payload is required.");
            }

            if (!CanAccessUser(id))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ProfileUpdate", "UserProfile", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "forbidden"
                });
                return Forbid();
            }

            var user = await _db.Users.FirstOrDefaultAsync(existing => existing.Id == id);
            if (user == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ProfileUpdate", "UserProfile", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "user_not_found"
                });
                return NotFound("User not found.");
            }

            var nextEmail = string.IsNullOrWhiteSpace(payload.Email)
                ? user.Email
                : payload.Email.Trim().ToLowerInvariant();

            var duplicate = await _db.Users.FirstOrDefaultAsync(existing =>
                existing.Id != id &&
                (existing.Email ?? "").ToLower() == nextEmail);

            if (duplicate != null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ProfileUpdate", "UserProfile", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "duplicate_email",
                    ["email"] = nextEmail
                }, user.Id, user.Role);
                return Conflict("Email already registered.");
            }

            var passwordChanged = !string.IsNullOrWhiteSpace(payload.Password);
            user.Name = string.IsNullOrWhiteSpace(payload.Name) ? user.Name : payload.Name.Trim();
            user.Email = nextEmail;
            user.Phone = payload.Phone?.Trim() ?? user.Phone;
            user.City = payload.City?.Trim() ?? user.City;

            var requesterRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (string.Equals(requesterRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(payload.Role))
            {
                user.Role = payload.Role.Trim();
            }

            if (!string.IsNullOrWhiteSpace(payload.Password))
            {
                if (payload.Password.Trim().Length < 6)
                {
                    await _auditLogService.WriteAsync(User, HttpContext, "PasswordChange", "UserProfile", id.ToString(), "Failure", new Dictionary<string, object?>
                    {
                        ["reason"] = "password_too_short"
                    }, user.Id, user.Role);
                    return BadRequest("Password must be at least 6 characters.");
                }

                user.Password = PasswordService.HashPassword(payload.Password.Trim());
            }

            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync(User, HttpContext, "ProfileUpdate", "UserProfile", id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["email"] = user.Email,
                ["role"] = user.Role
            }, user.Id, user.Role);

            if (passwordChanged)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "PasswordChange", "UserProfile", id.ToString(), "Success", new Dictionary<string, object?>
                {
                    ["message"] = "Password updated"
                }, user.Id, user.Role);
            }

            return Ok(ToPublicUser(user));
        }

        private bool CanAccessUser(int targetUserId)
        {
            var requesterRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (string.Equals(requesterRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var requesterId = GetCurrentUserId();
            return requesterId.HasValue && requesterId.Value == targetUserId;
        }

        private int? GetCurrentUserId()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var id) ? id : null;
        }

        private static object ToPublicUser(User user)
        {
            return new
            {
                user.Id,
                user.Name,
                user.Email,
                user.Phone,
                user.City,
                user.Role,
                user.CreatedAt
            };
        }
    }
}
