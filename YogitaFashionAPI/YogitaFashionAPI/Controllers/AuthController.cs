using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        public static List<User> UserStore => users;

        private static readonly List<User> users = new()
        {
            new User
            {
                Id = 1,
                Name = "Admin User",
                Email = "admin@yogitafashion.com",
                Password = "admin123",
                Phone = "9876543210",
                City = "Chennai",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            }
        };

        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("users")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetUsers()
        {
            var items = users
                .OrderByDescending(user => user.CreatedAt)
                .Select(ToPublicUser)
                .ToList();

            return Ok(items);
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public IActionResult Register(User user)
        {
            var email = (user.Email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest("Email and password are required.");
            }

            if (users.Any(existing => string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict("Email already registered.");
            }

            var requesterRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var requestedRole = (user.Role ?? "").Trim();
            var role = string.Equals(requesterRole, "Admin", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(requestedRole)
                    ? requestedRole
                    : "Customer";

            user.Id = users.Count == 0 ? 1 : users.Max(existing => existing.Id) + 1;
            user.Email = email;
            user.Name = (user.Name ?? "").Trim();
            user.Phone = (user.Phone ?? "").Trim();
            user.City = (user.City ?? "").Trim();
            user.Role = role;
            user.CreatedAt = user.CreatedAt == default ? DateTime.UtcNow : user.CreatedAt;

            users.Add(user);

            var token = JwtTokenService.GenerateToken(user, _configuration);
            return Ok(new
            {
                token,
                user = ToPublicUser(user)
            });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login(User loginUser)
        {
            var email = (loginUser.Email ?? "").Trim().ToLowerInvariant();
            var password = loginUser.Password ?? "";

            var user = users.FirstOrDefault(existing =>
                string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Password, password, StringComparison.Ordinal));

            if (user == null)
            {
                return Unauthorized("Invalid email or password");
            }

            var token = JwtTokenService.GenerateToken(user, _configuration);
            return Ok(new
            {
                token,
                user = ToPublicUser(user)
            });
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var user = users.FirstOrDefault(existing => existing.Id == currentUserId.Value);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            return Ok(ToPublicUser(user));
        }

        [HttpGet("profile/{id:int}")]
        [Authorize]
        public IActionResult GetProfile(int id)
        {
            if (!CanAccessUser(id))
            {
                return Forbid();
            }

            var user = users.FirstOrDefault(existing => existing.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(ToPublicUser(user));
        }

        [HttpPut("profile/{id:int}")]
        [Authorize]
        public IActionResult UpdateProfile(int id, User payload)
        {
            if (!CanAccessUser(id))
            {
                return Forbid();
            }

            var user = users.FirstOrDefault(existing => existing.Id == id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var nextEmail = string.IsNullOrWhiteSpace(payload.Email)
                ? user.Email
                : payload.Email.Trim().ToLowerInvariant();

            var duplicate = users.FirstOrDefault(existing =>
                existing.Id != id &&
                string.Equals(existing.Email, nextEmail, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
            {
                return Conflict("Email already registered.");
            }

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
                user.Password = payload.Password;
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
