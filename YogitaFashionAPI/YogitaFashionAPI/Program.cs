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
var allowedOrigins = localOrigins
    .Concat(configuredOrigins)
    .Concat(envOrigins)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

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
        policy
            .WithOrigins(allowedOrigins)
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
