import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import PageHeader from "../components/ui/PageHeader";
import StatCard from "../components/ui/StatCard";
import LoadingState from "../components/ui/LoadingState";
import { getDashboardSummary } from "../services/dashboardService";
import { notifyStockAlerts } from "../services/productService";
import { API } from "../api/endpoints";
import { downloadCsv } from "../services/exportService";
import { formatCurrency, formatDateTime } from "../utils/formatters";

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

  const peakDailyAmount = summary.dailySales.reduce((max, day) => Math.max(max, Number(day?.amount || 0)), 0);

  const handleExport = async (endpoint, fileName) => {
    try {
      await downloadCsv(endpoint, fileName);
      setActionMessage(`Downloaded ${fileName}`);
    } catch {
      setActionMessage("Export failed. Please try again.");
    }
  };

  return (
    <section>
      <PageHeader
        title="Dashboard Overview"
        description="Quick snapshot of your store performance."
        action={
          <div className="table-actions">
            <button type="button" className="btn btn-outline btn-sm" onClick={() => handleExport(API.EXPORT_ORDERS, "orders-export.csv")}>
              Export Orders CSV
            </button>
            <button type="button" className="btn btn-outline btn-sm" onClick={() => handleExport(API.EXPORT_USERS, "users-export.csv")}>
              Export Users CSV
            </button>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => handleExport(API.EXPORT_PRODUCTS, "products-export.csv")}
            >
              Export Products CSV
            </button>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => handleExport(API.EXPORT_RETURNS, "returns-export.csv")}
            >
              Export Returns CSV
            </button>
          </div>
        }
      />

      <div className="stats-grid">
        <StatCard label="Total Products" value={summary.totalProducts} helper="Products in catalog" tone="blue" />
        <StatCard label="Total Orders" value={summary.totalOrders} helper="All customer orders" tone="green" />
        <StatCard label="Total Users" value={summary.totalUsers} helper="Registered users" tone="orange" />
        <StatCard label="Revenue / Sales" value={formatCurrency(summary.revenue)} helper="Total order value" tone="pink" />
        <StatCard label="Out of Stock" value={summary.outOfStockCount} helper="Need stock refill" tone="orange" />
        <StatCard label="Notify Requests" value={summary.pendingAlertCount} helper="Pending customer alerts" tone="blue" />
        <StatCard label="Repeat Customers" value={summary.repeatCustomerCount} helper="Placed more than one order" tone="green" />
      </div>

      <div className="dashboard-grid">
        <div className="panel">
          <div className="panel-head">
            <h2>Recent Orders</h2>
            <Link to="/orders" className="text-link">
              View all
            </Link>
          </div>

          {summary.recentOrders.length === 0 ? (
            <p className="empty-text">No orders yet.</p>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>Order ID</th>
                    <th>Customer</th>
                    <th>Total</th>
                    <th>Status</th>
                    <th>Date</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.recentOrders.map((order) => (
                    <tr key={order.id}>
                      <td>{order.orderNumber || `#${order.id}`}</td>
                      <td>{order.customerName || "-"}</td>
                      <td>{formatCurrency(order.total)}</td>
                      <td>
                        <span className={`status-badge status-${String(order.status || "").toLowerCase()}`}>
                          {order.status}
                        </span>
                      </td>
                      <td>{formatDateTime(order.createdAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <div className="panel">
          <div className="panel-head">
            <h2>Out Of Stock Products</h2>
            <Link to="/products" className="text-link">
              Manage products
            </Link>
          </div>

          {summary.outOfStockProducts.length === 0 ? (
            <p className="empty-text">No product is out of stock.</p>
          ) : (
            <ul className="stock-list">
              {summary.outOfStockProducts.map((product) => (
                <li key={product.id}>
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
      </div>

      <div className="dashboard-grid">
        <div className="panel">
          <div className="panel-head">
            <h2>Sales Trend (Last 7 Days)</h2>
            <p className="panel-toolbar-text">Daily revenue from recent orders</p>
          </div>
          {summary.dailySales.length === 0 ? (
            <p className="empty-text">No sales data yet.</p>
          ) : (
            <div className="bars-wrap">
              {summary.dailySales.map((day) => {
                const amount = Number(day?.amount || 0);
                const height = peakDailyAmount > 0 ? Math.max(8, Math.round((amount / peakDailyAmount) * 120)) : 8;
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

        <div className="panel">
          <div className="panel-head">
            <h2>Top Products</h2>
            <p className="panel-toolbar-text">By quantity sold</p>
          </div>
          {summary.topProducts.length === 0 ? (
            <p className="empty-text">No top products yet.</p>
          ) : (
            <ul className="stock-list">
              {summary.topProducts.map((item) => (
                <li key={`${item.productId}-${item.productName}`}>
                  <div>
                    <p className="stock-name">{item.productName}</p>
                    <p className="stock-meta">Qty: {item.qty}</p>
                  </div>
                  <span className="stock-pill">{formatCurrency(item.revenue)}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="panel">
        <div className="panel-head">
          <h2>Availability Alert Queue</h2>
          <p className="panel-toolbar-text">Send alerts when a product is back in stock.</p>
        </div>

        {actionMessage ? <p className="inline-success">{actionMessage}</p> : null}

        {summary.restockedProductsWithAlerts.length === 0 ? (
          <p className="empty-text">No restocked product has pending customer alerts.</p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Stock</th>
                  <th>Pending Alerts</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {summary.restockedProductsWithAlerts.map((product) => (
                  <tr key={product.id}>
                    <td>{product.name}</td>
                    <td>{product.stock}</td>
                    <td>{product.pendingAlertCount}</td>
                    <td>
                      <button
                        type="button"
                        className="btn btn-sm btn-primary"
                        onClick={() => handleSendAlerts(product.id)}
                        disabled={sendingProductId === product.id}
                      >
                        {sendingProductId === product.id ? "Sending..." : "Send Alert"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}
