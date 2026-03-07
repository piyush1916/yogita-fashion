import { getProducts } from "./productService";
import { getOrders } from "./orderService";
import { getUsers } from "./userService";
import { getStockAlerts } from "./productService";
import { getAnalyticsSummary } from "./analyticsService";

export async function getDashboardSummary() {
  const [products, orders, users, pendingAlerts, analytics] = await Promise.all([
    getProducts(),
    getOrders(),
    getUsers(),
    getStockAlerts({ pendingOnly: true }),
    getAnalyticsSummary(7),
  ]);

  const revenue = orders.reduce((sum, order) => sum + Number(order.total || 0), 0);
  const outOfStockProducts = products.filter((product) => Number(product.stock) <= 0);

  const pendingAlertByProduct = pendingAlerts.reduce((acc, alert) => {
    const key = String(alert.productId || "");
    if (!key) return acc;
    acc[key] = (acc[key] || 0) + 1;
    return acc;
  }, {});

  const restockedProductsWithAlerts = products
    .filter((product) => Number(product.stock) > 0 && Number(pendingAlertByProduct[product.id] || 0) > 0)
    .map((product) => ({
      ...product,
      pendingAlertCount: Number(pendingAlertByProduct[product.id] || 0),
    }))
    .slice(0, 8);

  return {
    totalProducts: products.length,
    totalOrders: orders.length,
    totalUsers: users.length,
    revenue,
    outOfStockCount: outOfStockProducts.length,
    pendingAlertCount: pendingAlerts.length,
    recentOrders: orders.slice(0, 5),
    lowStockProducts: products.filter((product) => Number(product.stock) <= 5).slice(0, 5),
    outOfStockProducts: outOfStockProducts.slice(0, 8),
    pendingAlerts: pendingAlerts.slice(0, 10),
    restockedProductsWithAlerts,
    dailySales: Array.isArray(analytics?.dailySales) ? analytics.dailySales : [],
    topProducts: Array.isArray(analytics?.topProducts) ? analytics.topProducts : [],
    repeatCustomers: Array.isArray(analytics?.repeatCustomers) ? analytics.repeatCustomers : [],
    repeatCustomerCount: Number(analytics?.repeatCustomerCount || 0),
    statusBreakdown: Array.isArray(analytics?.statusBreakdown) ? analytics.statusBreakdown : [],
  };
}
