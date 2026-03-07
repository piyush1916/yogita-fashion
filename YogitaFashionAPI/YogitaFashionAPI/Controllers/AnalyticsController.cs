using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YogitaFashionAPI.Controllers
{
    [ApiController]
    [Route("analytics")]
    [Authorize(Policy = "AdminOnly")]
    public class AnalyticsController : ControllerBase
    {
        [HttpGet("summary")]
        public IActionResult GetSummary([FromQuery] int days = 7)
        {
            var safeDays = Math.Clamp(days, 3, 60);
            var startDate = DateTime.UtcNow.Date.AddDays(-(safeDays - 1));
            var orders = OrdersController.OrderStore;
            var users = AuthController.UserStore;
            var products = ProductsController.ProductStore;

            var filteredOrders = orders.Where(order => order.CreatedAt.Date >= startDate).ToList();
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

            var topProducts = filteredOrders
                .SelectMany(order => order.Items)
                .GroupBy(item => item.ProductId)
                .Select(group =>
                {
                    var qty = group.Sum(item => Math.Max(1, item.Qty));
                    var revenue = group.Sum(item => item.Price * Math.Max(1, item.Qty));
                    var productId = group.Key;
                    var productName = products.FirstOrDefault(item => item.Id.ToString() == productId)?.Name
                        ?? group.FirstOrDefault()?.Title
                        ?? "Unknown Product";
                    return new
                    {
                        productId,
                        productName,
                        qty,
                        revenue
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
                        : order.Phone.Trim())
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
                totalUsers = users.Count,
                totalProducts = products.Count,
                repeatCustomerCount = repeatCustomers.Count,
                dailySales,
                topProducts,
                repeatCustomers,
                statusBreakdown
            });
        }
    }
}
