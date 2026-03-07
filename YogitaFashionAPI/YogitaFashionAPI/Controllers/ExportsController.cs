using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [ApiController]
    [Route("exports")]
    [Authorize(Policy = "AdminOnly")]
    public class ExportsController : ControllerBase
    {
        [HttpGet("orders.csv")]
        public IActionResult ExportOrders()
        {
            var orders = OrdersController.OrderStore.OrderByDescending(item => item.CreatedAt).ToList();
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
        public IActionResult ExportUsers()
        {
            var users = AuthController.UserStore.OrderByDescending(item => item.CreatedAt).ToList();
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
        public IActionResult ExportProducts()
        {
            var products = ProductsController.ProductStore.OrderByDescending(item => item.Id).ToList();
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
        public IActionResult ExportReturns()
        {
            var rows = ReturnsController.ReturnStore
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new[]
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
