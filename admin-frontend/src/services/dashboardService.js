import { getProducts } from "./productService";
import { getOrders } from "./orderService";
import { getUsers } from "./userService";
import { getStockAlerts } from "./productService";
import { getAnalyticsSummary } from "./analyticsService";

function resolveError(label, error) {
  const status = Number(error?.response?.status || 0);
  const responseMessage =
    typeof error?.response?.data?.message === "string"
      ? error.response.data.message
      : typeof error?.response?.data === "string"
      ? error.response.data
      : "";
  const fallbackMessage = String(error?.message || "Request failed").trim();
  const message = responseMessage || fallbackMessage;
  return status > 0 ? `${label} (${status})${message ? `: ${message}` : ""}` : `${label}: ${message}`;
}

export async function getDashboardSummary() {
  const [productsResult, ordersResult, usersResult, alertsResult, analyticsResult] = await Promise.allSettled([
    getProducts(),
    getOrders(),
    getUsers(),
    getStockAlerts({ pendingOnly: true }),
    getAnalyticsSummary(7),
  ]);

  const warnings = [];
  const products = productsResult.status === "fulfilled" ? productsResult.value : [];
  if (productsResult.status === "rejected") {
    warnings.push(resolveError("Products", productsResult.reason));
  }

  const orders = ordersResult.status === "fulfilled" ? ordersResult.value : [];
  if (ordersResult.status === "rejected") {
    warnings.push(resolveError("Orders", ordersResult.reason));
  }

  const users = usersResult.status === "fulfilled" ? usersResult.value : [];
  if (usersResult.status === "rejected") {
    warnings.push(resolveError("Users", usersResult.reason));
  }

  const pendingAlerts = alertsResult.status === "fulfilled" ? alertsResult.value : [];
  if (alertsResult.status === "rejected") {
    warnings.push(resolveError("Stock alerts", alertsResult.reason));
  }

  const analytics = analyticsResult.status === "fulfilled" ? analyticsResult.value : null;
  if (analyticsResult.status === "rejected") {
    warnings.push(resolveError("Analytics summary", analyticsResult.reason));
  }

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
    warnings,
  };
}
