using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;

var builder = WebApplication.CreateBuilder(args);
var frontendCorsPolicy = "FrontendCors";
var defaultFrontendBaseUrl = "http://127.0.0.1:5173";
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YogitaFashion_SuperSecret_ChangeThisInProduction_2026";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "YogitaFashionAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "YogitaFashionClients";
var backendUrl = builder.Configuration["BackendUrl"] ?? "http://127.0.0.1:5037";
var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"] ?? defaultFrontendBaseUrl;
var defaultConnection =
    builder.Configuration.GetConnectionString("DefaultConnection") ??
    Environment.GetEnvironmentVariable("MYSQLCONNSTR_DefaultConnection") ??
    "server=localhost;port=3306;database=yogita_fashion_db;user=root;password=YOURPASSWORD;";
var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
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
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(defaultConnection, new MySqlServerVersion(new Version(8, 0, 34))));
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
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
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
    await db.Database.EnsureCreatedAsync();
    await EnsureDatabaseCompatibilityAsync(db);
    await SeedDefaultsAsync(db);
}

if (app.Environment.IsDevelopment())
{
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors(frontendCorsPolicy);
app.UseStaticFiles();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task EnsureDatabaseCompatibilityAsync(AppDbContext db)
{
    var statements = new[]
    {
        "ALTER TABLE products ADD COLUMN IF NOT EXISTS SizesJson LONGTEXT NULL;",
        "ALTER TABLE products ADD COLUMN IF NOT EXISTS ColorsJson LONGTEXT NULL;",
        "UPDATE products SET SizesJson = JSON_ARRAY(TRIM(Size)) WHERE (SizesJson IS NULL OR SizesJson = '') AND Size IS NOT NULL AND TRIM(Size) <> '';",
        "UPDATE products SET ColorsJson = JSON_ARRAY(TRIM(Color)) WHERE (ColorsJson IS NULL OR ColorsJson = '') AND Color IS NOT NULL AND TRIM(Color) <> '';",

        "ALTER TABLE orders ADD COLUMN IF NOT EXISTS ItemsJson LONGTEXT NULL;",
        "ALTER TABLE orders ADD COLUMN IF NOT EXISTS StatusHistoryJson LONGTEXT NULL;",
        "UPDATE orders SET ItemsJson = '[]' WHERE ItemsJson IS NULL OR ItemsJson = '';",
        "UPDATE orders SET StatusHistoryJson = '[]' WHERE StatusHistoryJson IS NULL OR StatusHistoryJson = '';",

        "ALTER TABLE stockalertsubscriptions ADD COLUMN IF NOT EXISTS WhatsAppNumber VARCHAR(30) NULL;",

        "ALTER TABLE coupons ADD COLUMN IF NOT EXISTS Type VARCHAR(20) NOT NULL DEFAULT 'percent';",
        "ALTER TABLE coupons ADD COLUMN IF NOT EXISTS Value DECIMAL(10,2) NOT NULL DEFAULT 0;",
        "ALTER TABLE coupons ADD COLUMN IF NOT EXISTS MaxUsesPerUser INT NOT NULL DEFAULT 1;",
        "ALTER TABLE coupons ADD COLUMN IF NOT EXISTS StartAt DATETIME NULL;",
        "UPDATE coupons SET Value = IFNULL(DiscountPercent, 0) WHERE Value = 0;",
        "UPDATE coupons SET Type = 'percent' WHERE Type IS NULL OR TRIM(Type) = '';",
        "UPDATE coupons SET MaxUsesPerUser = 1 WHERE MaxUsesPerUser IS NULL OR MaxUsesPerUser <= 0;",

        "ALTER TABLE supportrequests ADD COLUMN IF NOT EXISTS OrderReference VARCHAR(120) NULL;",
        "UPDATE supportrequests SET OrderReference = CAST(OrderId AS CHAR) WHERE (OrderReference IS NULL OR OrderReference = '') AND OrderId IS NOT NULL;",

        "ALTER TABLE returnrequests ADD COLUMN IF NOT EXISTS ItemProductCode VARCHAR(120) NULL;",
        "UPDATE returnrequests SET ItemProductCode = CAST(ItemProductId AS CHAR) WHERE (ItemProductCode IS NULL OR ItemProductCode = '') AND ItemProductId IS NOT NULL;",

        @"CREATE TABLE IF NOT EXISTS couponusagerecords (
            CouponId INT NOT NULL,
            UserId INT NOT NULL,
            Count INT NOT NULL DEFAULT 0,
            PRIMARY KEY (CouponId, UserId)
          ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;"
    };

    foreach (var statement in statements)
    {
        await db.Database.ExecuteSqlRawAsync(statement);
    }
}

static async Task SeedDefaultsAsync(AppDbContext db)
{
    var now = DateTime.UtcNow;

    if (!await db.Users.AnyAsync(user => user.Role == "Admin"))
    {
        db.Users.Add(new User
        {
            Name = "Admin User",
            Email = "admin@yogitafashion.com",
            Password = "admin123",
            Phone = "9876543210",
            City = "Chennai",
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
