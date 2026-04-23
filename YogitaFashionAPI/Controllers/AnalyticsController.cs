using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Controllers
{
    [ApiController]
    [Route("analytics")]
    [Authorize(Policy = "AdminOnly")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AnalyticsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int days = 7)
        {
            var safeDays = Math.Clamp(days, 3, 60);
            var startDate = DateTime.UtcNow.Date.AddDays(-(safeDays - 1));

            var orders = await _db.Orders.AsNoTracking().ToListAsync();
            var usersCount = await _db.Users.CountAsync();
            var productsCount = await _db.Products.CountAsync();

            var filteredOrders = orders
                .Where(order => order.CreatedAt.Date >= startDate)
                .ToList();

            var dailySales = Enumerable.Range(0, safeDays)
                .Select(offset =>
                {
                    var date = startDate.AddDays(offset);
                    var dayOrders = filteredOrders.Where(order => order.CreatedAt.Date == date).ToList();
                    var amount = dayOrders.Sum(order => order.Total);
                    return new
                    {
                        date = date.ToString("yyyy-MM-dd"),
                        amount,
                        orders = dayOrders.Count
                    };
                })
                .ToList();

            var groupedTopProducts = filteredOrders
                .SelectMany(order => order.Items ?? new List<OrderItem>())
                .GroupBy(item => item.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    Qty = group.Sum(item => Math.Max(1, item.Qty)),
                    Revenue = group.Sum(item => item.Price * Math.Max(1, item.Qty)),
                    FallbackTitle = group.FirstOrDefault()?.Title ?? "Unknown Product"
                })
                .ToList();

            var topProductIds = groupedTopProducts
                .Select(item => item.ProductId)
                .Where(item => int.TryParse(item, out _))
                .Select(item => int.Parse(item))
                .Distinct()
                .ToList();

            var productNames = topProductIds.Count == 0
                ? new Dictionary<int, string>()
                : await _db.Products
                    .Where(item => topProductIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id, item => item.Name);

            var topProducts = groupedTopProducts
                .Select(item =>
                {
                    var hasNumericId = int.TryParse(item.ProductId, out var numericId);
                    var productName = hasNumericId && productNames.TryGetValue(numericId, out var knownName)
                        ? knownName
                        : item.FallbackTitle;

                    return new
                    {
                        productId = item.ProductId,
                        productName,
                        qty = item.Qty,
                        revenue = item.Revenue
                    };
                })
                .OrderByDescending(item => item.qty)
                .ThenByDescending(item => item.revenue)
                .Take(6)
                .ToList();

            var repeatCustomers = orders
                .GroupBy(order =>
                    !string.IsNullOrWhiteSpace(order.Email)
                        ? order.Email.Trim().ToLowerInvariant()
                        : (order.Phone ?? "").Trim())
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .Select(group => new
                {
                    contact = group.Key,
                    orders = group.Count(),
                    totalSpent = group.Sum(item => item.Total)
                })
                .Where(item => item.orders > 1)
                .OrderByDescending(item => item.orders)
                .ThenByDescending(item => item.totalSpent)
                .Take(10)
                .ToList();

            var statusBreakdown = orders
                .GroupBy(order => string.IsNullOrWhiteSpace(order.Status) ? "Pending" : order.Status.Trim())
                .Select(group => new
                {
                    status = group.Key,
                    count = group.Count()
                })
                .OrderByDescending(item => item.count)
                .ToList();

            return Ok(new
            {
                days = safeDays,
                totalRevenue = orders.Sum(order => order.Total),
                totalOrders = orders.Count,
                totalUsers = usersCount,
                totalProducts = productsCount,
                repeatCustomerCount = repeatCustomers.Count,
                dailySales,
                topProducts,
                repeatCustomers,
                statusBreakdown
            });
        }
    }
}
