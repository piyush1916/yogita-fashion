using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Services
{
    public static class JwtTokenService
    {
        public static string GenerateToken(User user, IConfiguration configuration)
        {
            var key = configuration["Jwt:Key"] ?? "YogitaFashion_SuperSecret_ChangeThisInProduction_2026";
            var issuer = configuration["Jwt:Issuer"] ?? "YogitaFashionAPI";
            var audience = configuration["Jwt:Audience"] ?? "YogitaFashionClients";
            var expiresHours = configuration.GetValue<int?>("Jwt:ExpiresHours") ?? 24;

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
            };

            var signingCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expiresHours),
                signingCredentials: signingCredentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
