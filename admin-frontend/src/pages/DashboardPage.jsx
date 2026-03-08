import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import PageHeader from "../components/ui/PageHeader";
import LoadingState from "../components/ui/LoadingState";
import { getDashboardSummary } from "../services/dashboardService";
import { notifyStockAlerts } from "../services/productService";
import { API } from "../api/endpoints";
import { downloadCsv } from "../services/exportService";
import { formatCurrency, formatDateTime } from "../utils/formatters";

const STATUS_TONE_BY_NAME = {
  delivered: "good",
  shipped: "good",
  confirmed: "info",
  packed: "info",
  pending: "warn",
  returned: "warn",
  refunded: "info",
  cancelled: "danger",
  rejected: "danger",
};

function getStatusTone(status) {
  const key = String(status || "").trim().toLowerCase();
  return STATUS_TONE_BY_NAME[key] || "neutral";
}

export default function DashboardPage() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [actionMessage, setActionMessage] = useState("");
  const [sendingProductId, setSendingProductId] = useState("");
  const [summary, setSummary] = useState({
    totalProducts: 0,
    totalOrders: 0,
    totalUsers: 0,
    revenue: 0,
    outOfStockCount: 0,
    pendingAlertCount: 0,
    recentOrders: [],
    lowStockProducts: [],
    outOfStockProducts: [],
    pendingAlerts: [],
    restockedProductsWithAlerts: [],
    dailySales: [],
    topProducts: [],
    repeatCustomers: [],
    repeatCustomerCount: 0,
    statusBreakdown: [],
  });

  const loadSummary = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const data = await getDashboardSummary();
      setSummary(data);
    } catch (apiError) {
      const message =
        typeof apiError?.response?.data?.message === "string"
          ? apiError.response.data.message
          : typeof apiError?.response?.data === "string"
          ? apiError.response.data
          : "Failed to load dashboard summary.";
      setError(message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadSummary();
  }, [loadSummary]);

  const handleSendAlerts = async (productId) => {
    setActionMessage("");
    setSendingProductId(productId);
    try {
      const result = await notifyStockAlerts(productId);
      setActionMessage(result?.message || "Availability alerts sent.");
      await loadSummary();
    } catch (apiError) {
      const message =
        typeof apiError?.response?.data?.message === "string"
          ? apiError.response.data.message
          : typeof apiError?.response?.data === "string"
          ? apiError.response.data
          : "Failed to send availability alerts.";
      setActionMessage(message);
    } finally {
      setSendingProductId("");
    }
  };

  const handleExport = async (endpoint, fileName) => {
    try {
      await downloadCsv(endpoint, fileName);
      setActionMessage(`Downloaded ${fileName}`);
    } catch {
      setActionMessage("Export failed. Please try again.");
    }
  };

  const peakDailyAmount = summary.dailySales.reduce((max, day) => Math.max(max, Number(day?.amount || 0)), 0);
  const totalStatusCount = summary.statusBreakdown.reduce((sum, item) => sum + Number(item?.count || 0), 0);
  const deliveredCount = summary.statusBreakdown.reduce((sum, item) => {
    return String(item?.status || "").trim().toLowerCase() === "delivered" ? sum + Number(item?.count || 0) : sum;
  }, 0);
  const deliveryRate = summary.totalOrders > 0 ? Math.round((deliveredCount / summary.totalOrders) * 100) : 0;
  const averageOrderValue = summary.totalOrders > 0 ? summary.revenue / summary.totalOrders : 0;
  const averageDailySales =
    summary.dailySales.length > 0
      ? summary.dailySales.reduce((sum, day) => sum + Number(day?.amount || 0), 0) / summary.dailySales.length
      : 0;
  const topOrderStatus = summary.statusBreakdown[0];
  const lowStockCount = summary.lowStockProducts.length;

  const metricCards = useMemo(
    () => [
      {
        label: "Gross Revenue",
        value: formatCurrency(summary.revenue),
        helper: `Avg order ${formatCurrency(averageOrderValue)}`,
        tone: "revenue",
      },
      {
        label: "Orders Processed",
        value: summary.totalOrders,
        helper: `${deliveryRate}% delivered`,
        tone: "orders",
      },
      {
        label: "Customer Base",
        value: summary.totalUsers,
        helper: `${summary.repeatCustomerCount} repeat buyers`,
        tone: "users",
      },
      {
        label: "Inventory Risk",
        value: summary.outOfStockCount + lowStockCount,
        helper: `${summary.outOfStockCount} out, ${lowStockCount} low stock`,
        tone: "inventory",
      },
      {
        label: "Alert Queue",
        value: summary.pendingAlertCount,
        helper: `${summary.restockedProductsWithAlerts.length} ready to notify`,
        tone: "alerts",
      },
      {
        label: "Daily Sales Pulse",
        value: formatCurrency(averageDailySales),
        helper: "Average of last 7 days",
        tone: "pulse",
      },
    ],
    [
      summary.revenue,
      averageOrderValue,
      summary.totalOrders,
      deliveryRate,
      summary.totalUsers,
      summary.repeatCustomerCount,
      summary.outOfStockCount,
      lowStockCount,
      summary.pendingAlertCount,
      summary.restockedProductsWithAlerts.length,
      averageDailySales,
    ]
  );

  if (loading) {
    return (
      <section>
        <PageHeader title="Dashboard Overview" description="Quick snapshot of your store performance." />
        <LoadingState label="Loading dashboard..." />
      </section>
    );
  }

  if (error) {
    return (
      <section>
        <PageHeader title="Dashboard Overview" description="Quick snapshot of your store performance." />
        <div className="panel">
          <p className="form-error-banner">{error}</p>
        </div>
      </section>
    );
  }

  return (
    <section className="dashboard-v2">
      <PageHeader
        title="Digital Command Center"
        description="Live snapshot of store operations, sales velocity, and customer activity."
        action={
          <button type="button" className="btn btn-outline" onClick={loadSummary} disabled={loading}>
            Refresh Overview
          </button>
        }
      />

      {actionMessage ? <p className="inline-success">{actionMessage}</p> : null}

      <div className="dash2-hero panel">
        <div className="dash2-hero-main">
          <p className="dash2-kicker">Operations Snapshot</p>
          <h2>Store pulse looks {deliveryRate >= 70 ? "healthy" : "watchful"} today</h2>
          <p className="dash2-hero-copy">
            Track revenue, status movement, and inventory pressure in one place. Focus first on pending alerts and low stock items.
          </p>
          <div className="dash2-hero-stats">
            <div>
              <span>Primary status</span>
              <strong>{topOrderStatus ? `${topOrderStatus.status} (${topOrderStatus.count})` : "No order data"}</strong>
            </div>
            <div>
              <span>Total products</span>
              <strong>{summary.totalProducts}</strong>
            </div>
            <div>
              <span>Restocked with alerts</span>
              <strong>{summary.restockedProductsWithAlerts.length}</strong>
            </div>
          </div>
        </div>

        <aside className="dash2-export-card">
          <p className="dash2-export-title">Quick Exports</p>
          <button type="button" className="dash2-export-btn" onClick={() => handleExport(API.EXPORT_ORDERS, "orders-export.csv")}>
            Orders CSV
          </button>
          <button type="button" className="dash2-export-btn" onClick={() => handleExport(API.EXPORT_USERS, "users-export.csv")}>
            Users CSV
          </button>
          <button type="button" className="dash2-export-btn" onClick={() => handleExport(API.EXPORT_PRODUCTS, "products-export.csv")}>
            Products CSV
          </button>
          <button type="button" className="dash2-export-btn" onClick={() => handleExport(API.EXPORT_RETURNS, "returns-export.csv")}>
            Returns CSV
          </button>
        </aside>
      </div>

      <div className="dash2-metric-grid">
        {metricCards.map((item) => (
          <article key={item.label} className={`dash2-metric-card tone-${item.tone}`}>
            <p className="dash2-metric-label">{item.label}</p>
            <p className="dash2-metric-value">{item.value}</p>
            <p className="dash2-metric-helper">{item.helper}</p>
          </article>
        ))}
      </div>

      <div className="dash2-grid">
        <div className="panel dash2-panel">
          <div className="panel-head">
            <h2>Recent Orders Feed</h2>
            <Link to="/orders" className="text-link">
              View all
            </Link>
          </div>

          {summary.recentOrders.length === 0 ? (
            <p className="empty-text">No orders yet.</p>
          ) : (
            <ul className="dash2-order-feed">
              {summary.recentOrders.map((order) => (
                <li key={order.id} className="dash2-order-row">
                  <div className="dash2-order-left">
                    <p className="dash2-order-id">{order.orderNumber || `#${order.id}`}</p>
                    <p className="dash2-order-meta">{order.customerName || "Guest"} | {formatDateTime(order.createdAt)}</p>
                  </div>
                  <div className="dash2-order-right">
                    <strong>{formatCurrency(order.total)}</strong>
                    <span className={`status-badge status-${String(order.status || "").toLowerCase()}`}>{order.status}</span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="panel dash2-panel">
          <div className="panel-head">
            <h2>Status Mix</h2>
            <p className="panel-toolbar-text">{summary.totalOrders} total orders</p>
          </div>

          {summary.statusBreakdown.length === 0 ? (
            <p className="empty-text">No status data available.</p>
          ) : (
            <div className="dash2-status-list">
              {summary.statusBreakdown.map((item) => {
                const count = Number(item?.count || 0);
                const progress = totalStatusCount > 0 ? Math.max(6, Math.round((count / totalStatusCount) * 100)) : 0;
                const tone = getStatusTone(item?.status);

                return (
                  <div key={String(item?.status || "unknown")} className="dash2-status-item">
                    <div className="dash2-status-head">
                      <span className={`dash2-status-name tone-${tone}`}>{item.status}</span>
                      <strong>{count}</strong>
                    </div>
                    <div className="dash2-progress">
                      <span className={`dash2-progress-fill tone-${tone}`} style={{ width: `${progress}%` }} />
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>

      <div className="dash2-grid">
        <div className="panel dash2-panel">
          <div className="panel-head">
            <h2>Sales Wave (Last 7 Days)</h2>
            <p className="panel-toolbar-text">Bars represent day-wise revenue</p>
          </div>
          {summary.dailySales.length === 0 ? (
            <p className="empty-text">No sales data yet.</p>
          ) : (
            <div className="bars-wrap dash2-bars">
              {summary.dailySales.map((day) => {
                const amount = Number(day?.amount || 0);
                const height = peakDailyAmount > 0 ? Math.max(10, Math.round((amount / peakDailyAmount) * 128)) : 10;
                return (
                  <div key={day.date} className="bar-item">
                    <div className="bar-value">{formatCurrency(amount)}</div>
                    <div className="bar-track">
                      <div className="bar-fill" style={{ height: `${height}px` }} />
                    </div>
                    <div className="bar-label">{day.date?.slice(5) || "-"}</div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        <div className="panel dash2-panel">
          <div className="panel-head">
            <h2>Top Products</h2>
            <p className="panel-toolbar-text">Ranked by quantity sold</p>
          </div>
          {summary.topProducts.length === 0 ? (
            <p className="empty-text">No top products yet.</p>
          ) : (
            <ul className="dash2-top-list">
              {summary.topProducts.map((item, index) => (
                <li key={`${item.productId}-${item.productName}`}>
                  <div>
                    <p className="stock-name">{item.productName}</p>
                    <p className="stock-meta">Qty sold: {item.qty}</p>
                  </div>
                  <span className={`stock-pill ${index === 0 ? "dash2-rank-pill" : ""}`}>{formatCurrency(item.revenue)}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="dash2-grid">
        <div className="panel dash2-panel">
          <div className="panel-head">
            <h2>Inventory Radar</h2>
            <Link to="/products" className="text-link">
              Manage products
            </Link>
          </div>

          {summary.outOfStockProducts.length === 0 && summary.lowStockProducts.length === 0 ? (
            <p className="empty-text">Inventory is healthy. No low or out-of-stock items right now.</p>
          ) : (
            <div className="dash2-inventory-columns">
              <div>
                <p className="dash2-mini-title">Out of stock</p>
                {summary.outOfStockProducts.length === 0 ? (
                  <p className="empty-text">None</p>
                ) : (
                  <ul className="stock-list">
                    {summary.outOfStockProducts.map((product) => (
                      <li key={`out-${product.id}`}>
                        <div>
                          <p className="stock-name">{product.name}</p>
                          <p className="stock-meta">{product.category || "Uncategorized"}</p>
                        </div>
                        <span className="stock-pill stock-pill-danger">Out</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              <div>
                <p className="dash2-mini-title">Low stock (&lt;= 5)</p>
                {summary.lowStockProducts.length === 0 ? (
                  <p className="empty-text">None</p>
                ) : (
                  <ul className="stock-list">
                    {summary.lowStockProducts.map((product) => (
                      <li key={`low-${product.id}`}>
                        <div>
                          <p className="stock-name">{product.name}</p>
                          <p className="stock-meta">{product.category || "Uncategorized"}</p>
                        </div>
                        <span className="stock-pill">Stock {product.stock}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          )}
        </div>

        <div className="panel dash2-panel">
          <div className="panel-head">
            <h2>Availability Alert Queue</h2>
            <p className="panel-toolbar-text">Send alerts for products back in stock</p>
          </div>

          {summary.restockedProductsWithAlerts.length === 0 ? (
            <p className="empty-text">No restocked product has pending customer alerts.</p>
          ) : (
            <ul className="dash2-alert-list">
              {summary.restockedProductsWithAlerts.map((product) => (
                <li key={product.id}>
                  <div>
                    <p className="stock-name">{product.name}</p>
                    <p className="stock-meta">
                      Stock {product.stock} | {product.pendingAlertCount} waiting
                    </p>
                  </div>
                  <button
                    type="button"
                    className="btn btn-sm btn-primary"
                    onClick={() => handleSendAlerts(product.id)}
                    disabled={sendingProductId === product.id}
                  >
                    {sendingProductId === product.id ? "Sending..." : "Send Alert"}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="dash2-grid">
        <div className="panel dash2-panel">
          <div className="panel-head">
            <h2>Repeat Customers</h2>
            <p className="panel-toolbar-text">Most frequent buyers</p>
          </div>
          {summary.repeatCustomers.length === 0 ? (
            <p className="empty-text">No repeat customers yet.</p>
          ) : (
            <ul className="dash2-repeat-list">
              {summary.repeatCustomers.map((customer) => (
                <li key={customer.contact}>
                  <p className="stock-name">{customer.contact}</p>
                  <p className="stock-meta">
                    Orders: {customer.orders} | Spent: {formatCurrency(customer.totalSpent)}
                  </p>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="panel dash2-panel dash2-shortcut-panel">
          <div className="panel-head">
            <h2>Team Shortcuts</h2>
          </div>
          <div className="dash2-shortcuts">
            <Link to="/products/new" className="dash2-shortcut">
              <strong>Add Product</strong>
              <span>Create new SKU and publish catalog updates.</span>
            </Link>
            <Link to="/orders" className="dash2-shortcut">
              <strong>Review Orders</strong>
              <span>Update statuses and track fulfilment queue.</span>
            </Link>
            <Link to="/returns" className="dash2-shortcut">
              <strong>Handle Returns</strong>
              <span>Approve, reject, or mark refunds quickly.</span>
            </Link>
            <Link to="/audit-logs" className="dash2-shortcut">
              <strong>Audit Trail</strong>
              <span>View recent admin actions and accountability logs.</span>
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
