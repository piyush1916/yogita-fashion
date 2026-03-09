using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [ApiController]
    [Route("exports")]
    [Authorize(Policy = "AdminOnly")]
    public class ExportsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ExportsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("orders.csv")]
        public async Task<IActionResult> ExportOrders()
        {
            var orders = await _db.Orders
                .AsNoTracking()
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync();

            var rows = orders.Select(order => new[]
            {
                order.Id.ToString(),
                order.UserId.ToString(),
                order.Name,
                order.Email,
                order.Phone,
                order.City,
                order.Total.ToString("0.##"),
                order.Status,
                order.Payment,
                order.TrackingNumber,
                order.CreatedAt.ToString("u")
            });

            var bytes = CsvExportService.BuildCsv(
                new[] { "OrderId", "UserId", "Name", "Email", "Phone", "City", "Total", "Status", "Payment", "Tracking", "CreatedAtUtc" },
                rows
            );
            return File(bytes, "text/csv", $"orders-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        [HttpGet("users.csv")]
        public async Task<IActionResult> ExportUsers()
        {
            var users = await _db.Users
                .AsNoTracking()
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync();

            var rows = users.Select(user => new[]
            {
                user.Id.ToString(),
                user.Name,
                user.Email,
                user.Phone,
                user.City,
                user.Role,
                user.CreatedAt.ToString("u")
            });

            var bytes = CsvExportService.BuildCsv(
                new[] { "UserId", "Name", "Email", "Phone", "City", "Role", "CreatedAtUtc" },
                rows
            );
            return File(bytes, "text/csv", $"users-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        [HttpGet("products.csv")]
        public async Task<IActionResult> ExportProducts()
        {
            var products = await _db.Products
                .AsNoTracking()
                .OrderByDescending(item => item.Id)
                .ToListAsync();

            var rows = products.Select(product => new[]
            {
                product.Id.ToString(),
                product.Name,
                product.Category,
                product.Brand,
                product.Price.ToString("0.##"),
                product.OriginalPrice.ToString("0.##"),
                product.Stock.ToString(),
                product.FeaturedProduct ? "Yes" : "No"
            });

            var bytes = CsvExportService.BuildCsv(
                new[] { "ProductId", "Name", "Category", "Brand", "Price", "OriginalPrice", "Stock", "Featured" },
                rows
            );
            return File(bytes, "text/csv", $"products-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        [HttpGet("returns.csv")]
        public async Task<IActionResult> ExportReturns()
        {
            var requests = await _db.ReturnRequests
                .AsNoTracking()
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync();

            var rows = requests.Select(item => new[]
            {
                item.Id.ToString(),
                item.OrderId.ToString(),
                item.UserId.ToString(),
                item.ItemProductId,
                item.ItemTitle,
                item.Quantity.ToString(),
                item.RefundAmount.ToString("0.##"),
                item.Status,
                item.Reason,
                item.CreatedAt.ToString("u")
            });

            var bytes = CsvExportService.BuildCsv(
                new[] { "ReturnId", "OrderId", "UserId", "ProductId", "ItemTitle", "Quantity", "RefundAmount", "Status", "Reason", "CreatedAtUtc" },
                rows
            );
            return File(bytes, "text/csv", $"returns-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }
    }
}
