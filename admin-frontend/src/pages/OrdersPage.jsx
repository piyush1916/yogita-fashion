import { useEffect, useMemo, useState } from "react";
import PageHeader from "../components/ui/PageHeader";
import LoadingState from "../components/ui/LoadingState";
import { getOrders, updateOrderStatus } from "../services/orderService";
import { formatCurrency, formatDateTime } from "../utils/formatters";

function statusClass(status) {
  return `status-${String(status || "")
    .toLowerCase()
    .replace(/\s+/g, "-")}`;
}

export default function OrdersPage() {
  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [updatingId, setUpdatingId] = useState("");
  const [statusDrafts, setStatusDrafts] = useState({});

  useEffect(() => {
    let ignore = false;

    const loadOrders = async () => {
      setLoading(true);
      setError("");
      try {
        const data = await getOrders();
        if (!ignore) {
          setOrders(data);
        }
      } catch {
        if (!ignore) {
          setError("Failed to load orders.");
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    };

    loadOrders();
    return () => {
      ignore = true;
    };
  }, []);

  const setDraftStatus = (orderId, status) => {
    setStatusDrafts((prev) => ({ ...prev, [orderId]: status }));
  };

  const handleUpdateStatus = async (order) => {
    const nextStatus = statusDrafts[order.id] || order.status;
    if (!nextStatus || nextStatus === order.status) return;

    const notes = window.prompt(`Add note for ${nextStatus} (optional):`, "") ?? "";
    setUpdatingId(order.id);
    setError("");
    try {
      const updated = await updateOrderStatus(order.id, { status: nextStatus, notes });
      setOrders((prev) => prev.map((item) => (item.id === order.id ? updated : item)));
    } catch (apiError) {
      const message =
        typeof apiError?.response?.data?.message === "string"
          ? apiError.response.data.message
          : "Failed to update order status.";
      setError(message);
    } finally {
      setUpdatingId("");
    }
  };

  const filteredOrders = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return orders;

    return orders.filter((order) => {
      const searchSource = [
        order.id,
        order.orderNumber,
        order.customerName,
        order.email,
        order.phone,
        order.status,
        order.trackingNumber,
        order.payment,
      ]
        .join(" ")
        .toLowerCase();

      return searchSource.includes(term);
    });
  }, [orders, search]);

  return (
    <section>
      <PageHeader title="Orders" description="Track every order, payment type, and order status at a glance." />

      <div className="panel">
        <div className="panel-toolbar">
          <input
            className="search-input"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search by order id, customer, status, tracking..."
          />
          <p className="panel-toolbar-text">Showing {filteredOrders.length} orders</p>
        </div>

        {error ? <p className="form-error-banner">{error}</p> : null}

        {loading ? (
          <LoadingState label="Loading orders..." />
        ) : filteredOrders.length === 0 ? (
          <p className="empty-text">No orders found.</p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Order ID</th>
                  <th>Customer</th>
                  <th>Items</th>
                  <th>Total</th>
                  <th>Payment</th>
                  <th>Status</th>
                  <th>Tracking</th>
                  <th>Date</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {filteredOrders.map((order) => (
                  <tr key={order.id}>
                    <td>
                      <p>{order.orderNumber || `#${order.id}`}</p>
                      <p className="row-meta">Internal: #{order.id}</p>
                    </td>
                    <td>
                      <p>{order.customerName || "-"}</p>
                      <p className="row-meta">{order.email || order.phone || "-"}</p>
                    </td>
                    <td>{order.itemCount}</td>
                    <td>{formatCurrency(order.total)}</td>
                    <td>{order.payment}</td>
                    <td>
                      <span className={`status-badge ${statusClass(order.status)}`}>{order.status}</span>
                    </td>
                    <td>{order.trackingNumber || "-"}</td>
                    <td>{formatDateTime(order.createdAt)}</td>
                    <td>
                      <div className="table-actions">
                        <select
                          className="table-select"
                          value={statusDrafts[order.id] || order.status}
                          onChange={(event) => setDraftStatus(order.id, event.target.value)}
                        >
                          {["Pending", "Confirmed", "Packed", "Shipped", "Delivered", "Cancelled", "Returned", "Refunded", "Rejected"].map(
                            (status) => (
                              <option key={status} value={status}>
                                {status}
                              </option>
                            )
                          )}
                        </select>
                        <button
                          type="button"
                          className="btn btn-sm btn-primary"
                          onClick={() => handleUpdateStatus(order)}
                          disabled={updatingId === order.id || (statusDrafts[order.id] || order.status) === order.status}
                        >
                          {updatingId === order.id ? "Updating..." : "Update"}
                        </button>
                      </div>
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
