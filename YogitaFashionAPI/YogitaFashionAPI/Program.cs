using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var frontendCorsPolicy = "FrontendCors";
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YogitaFashion_SuperSecret_ChangeThisInProduction_2026";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "YogitaFashionAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "YogitaFashionClients";
var backendUrl = builder.Configuration["BackendUrl"] ?? "http://127.0.0.1:5037";
var renderPortRaw = Environment.GetEnvironmentVariable("PORT");
var renderPort = int.TryParse(renderPortRaw, out var parsedRenderPort) ? parsedRenderPort : 0;
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
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var envOriginsRaw = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "";
var envOrigins = envOriginsRaw
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
var allowedOrigins = (builder.Environment.IsDevelopment() ? localOrigins : Array.Empty<string>())
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
builder.WebHost.UseWebRoot(webRootPath);

// Render expects binding to 0.0.0.0:$PORT.
if (renderPort > 0)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}
else
{
    builder.WebHost.UseUrls(backendUrl);
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
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
