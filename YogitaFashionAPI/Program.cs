using System.Text;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var frontendCorsPolicy = "FrontendCors";
var defaultFrontendBaseUrl = "http://127.0.0.1:5173";
var jwtKey = builder.Configuration["Jwt:Key"];
var auditIntegrityKey = builder.Configuration["Audit:IntegrityKey"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("Jwt:Key must be configured.");
}

if (!builder.Environment.IsDevelopment() &&
    jwtKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Jwt:Key uses an insecure placeholder value.");
}

if (!builder.Environment.IsDevelopment() &&
    !string.IsNullOrWhiteSpace(auditIntegrityKey) &&
    auditIntegrityKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Audit:IntegrityKey uses an insecure placeholder value.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "YogitaFashionAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "YogitaFashionClients";
var backendUrl = builder.Configuration["BackendUrl"] ?? "http://127.0.0.1:5037";
var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"] ?? defaultFrontendBaseUrl;
var defaultConnection =
    builder.Configuration.GetConnectionString("DefaultConnection") ??
    Environment.GetEnvironmentVariable("MYSQLCONNSTR_DefaultConnection");

if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");
}
if (!defaultConnection.Contains("AllowPublicKeyRetrieval", StringComparison.OrdinalIgnoreCase))
{
    defaultConnection = $"{defaultConnection.TrimEnd(';')};AllowPublicKeyRetrieval=True";
}

if (!defaultConnection.Contains("SslMode", StringComparison.OrdinalIgnoreCase))
{
    defaultConnection = $"{defaultConnection.TrimEnd(';')};SslMode=None";
}

if (!defaultConnection.Contains("AllowUserVariables", StringComparison.OrdinalIgnoreCase))
{
    defaultConnection = $"{defaultConnection.TrimEnd(';')};AllowUserVariables=True";
}

defaultConnection = $"{defaultConnection.TrimEnd(';')};";
var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, ".aspnet-data-protection-keys");
Directory.CreateDirectory(dataProtectionPath);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("YogitaFashionAPI");

Func<string?, string?> normalizeOrigin = rawOrigin =>
{
    var candidate = (rawOrigin ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(candidate))
    {
        return null;
    }

    if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        && !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        candidate = $"https://{candidate}";
    }

    if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsedOrigin))
    {
        return null;
    }

    if (!string.Equals(parsedOrigin.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(parsedOrigin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    return parsedOrigin.GetLeftPart(UriPartial.Authority).TrimEnd('/');
};
var localOrigins = new[]
{
    "http://localhost:5173",
    "http://127.0.0.1:5173",
    "http://localhost:5174",
    "http://127.0.0.1:5174"
};
var defaultOrigins = (builder.Environment.IsDevelopment() ? localOrigins : Array.Empty<string>())
    .Concat(new[] { frontendBaseUrl })
    .ToArray();
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var envOriginsRaw = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "";
var envOrigins = envOriginsRaw
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
var allowedOrigins = defaultOrigins
    .Concat(configuredOrigins)
    .Concat(envOrigins)
    .Select(normalizeOrigin)
    .OfType<string>()
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
var allowAnyOriginConfigured = builder.Configuration.GetValue("Cors:AllowAnyOrigin", false);
var allowAnyOrigin = allowAnyOriginConfigured || (!builder.Environment.IsDevelopment() && allowedOrigins.Length == 0);

// Keep static files stable across local/dev/prod hosts.
if (!Directory.Exists(webRootPath))
{
    Directory.CreateDirectory(webRootPath);
}

builder.WebHost.UseUrls(backendUrl);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(defaultConnection, new MySqlServerVersion(new Version(8, 0, 34))));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireAssertion(context =>
        context.User.Claims.Any(claim =>
            (claim.Type == ClaimTypes.Role || claim.Type == "role") &&
            string.Equals(claim.Value, "Admin", StringComparison.OrdinalIgnoreCase))));
});
builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        if (allowAnyOrigin)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedDefaultsAsync(db, builder.Configuration);
    Console.WriteLine("Database migrations and seed defaults completed.");
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors(frontendCorsPolicy);
app.UseStaticFiles();

app.UseForwardedHeaders();
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task SeedDefaultsAsync(AppDbContext db, IConfiguration configuration)
{
    var now = DateTime.UtcNow;
    var configuredAdminEmail = (configuration["Seed:AdminEmail"] ?? "admin@yogitafashion.com").Trim().ToLowerInvariant();
    var configuredAdminName = (configuration["Seed:AdminName"] ?? "Yogita Fashion Admin").Trim();
    var configuredAdminPhone = (configuration["Seed:AdminPhone"] ?? "").Trim();
    var configuredAdminCity = (configuration["Seed:AdminCity"] ?? "").Trim();
    var configuredAdminPassword = configuration["Seed:AdminPassword"] ?? "ChangeMe@123";
    var normalizedAdminPassword = configuredAdminPassword.Trim();
    if (string.IsNullOrWhiteSpace(normalizedAdminPassword))
    {
        normalizedAdminPassword = "ChangeMe@123";
    }

    var users = await db.Users.ToListAsync();
    foreach (var user in users)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            continue;
        }

        user.Email = user.Email.Trim().ToLowerInvariant();
        if (!PasswordService.IsPasswordHashed(user.Password))
        {
            user.Password = PasswordService.HashPassword(user.Password ?? string.Empty);
        }
    }

    var hasAdmin = await db.Users.AnyAsync(user => !string.IsNullOrWhiteSpace(user.Role) && user.Role.ToLower() == "admin");
    var seededAdmin = await db.Users.FirstOrDefaultAsync(user => user.Email == configuredAdminEmail);

    if (seededAdmin != null && !string.Equals(seededAdmin.Role, "Admin", StringComparison.OrdinalIgnoreCase))
    {
        seededAdmin.Role = "Admin";
    }

    if (seededAdmin != null && !PasswordService.IsPasswordHashed(seededAdmin.Password))
    {
        seededAdmin.Password = PasswordService.HashPassword(seededAdmin.Password ?? normalizedAdminPassword);
    }

    if (!hasAdmin && seededAdmin == null)
    {
        db.Users.Add(new User
        {
            Name = configuredAdminName,
            Email = configuredAdminEmail,
            Password = PasswordService.HashPassword(normalizedAdminPassword),
            Phone = configuredAdminPhone,
            City = configuredAdminCity,
            Role = "Admin",
            CreatedAt = now
        });
    }

    if (!await db.Coupons.AnyAsync())
    {
        db.Coupons.AddRange(
            new Coupon
            {
                Code = "SAVE10",
                Type = "percent",
                Value = 10,
                MinOrderAmount = 499,
                MaxUses = 1000,
                MaxUsesPerUser = 2,
                UsedCount = 0,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Coupon
            {
                Code = "FASHION200",
                Type = "fixed",
                Value = 200,
                MinOrderAmount = 1499,
                MaxUses = 500,
                MaxUsesPerUser = 1,
                UsedCount = 0,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
    }

    await db.SaveChangesAsync();
}
