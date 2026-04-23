import { useEffect, useMemo, useState } from "react";
import PageHeader from "../components/ui/PageHeader";
import LoadingState from "../components/ui/LoadingState";
import { getReturnRequests, updateReturnStatus } from "../services/returnService";
import { formatCurrency, formatDateTime } from "../utils/formatters";

const STATUS_OPTIONS = ["Pending", "Pickup Started", "Pickup Completed", "Refunded", "Rejected"];

const normalizeReturnStatus = (status) => String(status || "").trim().toLowerCase().replace(/[\s_-]+/g, "");

const getDisplayStatus = (status) => {
  const normalized = normalizeReturnStatus(status);
  if (!normalized) return "Pending";
  if (normalized === "approved" || normalized === "pickupstarted") return "Pickup Started";
  if (normalized === "completed" || normalized === "returned" || normalized === "pickupcompleted") {
    return "Pickup Completed";
  }
  if (normalized === "pending") return "Pending";
  if (normalized === "refunded") return "Refunded";
  if (normalized === "rejected") return "Rejected";
  return String(status || "").trim();
};

export default function ReturnsPage() {
  const [returns, setReturns] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");
  const [savingId, setSavingId] = useState("");

  const loadReturns = async () => {
    setLoading(true);
    setError("");
    try {
      const items = await getReturnRequests();
      setReturns(items);
    } catch {
      setError("Failed to load return requests.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadReturns();
  }, []);

  const filteredReturns = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return returns;
    return returns.filter((item) =>
      [item.id, item.orderId, item.itemTitle, item.reason, item.status, item.userId].join(" ").toLowerCase().includes(term)
    );
  }, [returns, search]);

  const handleStatusChange = async (item, status) => {
    const adminRemark = window.prompt("Optional admin remark:", item.adminRemark || "") ?? item.adminRemark;
    setSavingId(item.id);
    setError("");
    try {
      const updated = await updateReturnStatus(item.id, { status, adminRemark });
      setReturns((prev) => prev.map((entry) => (entry.id === item.id ? updated : entry)));
    } catch {
      setError("Failed to update return status.");
    } finally {
      setSavingId("");
    }
  };

  return (
    <section>
      <PageHeader title="Returns & Refunds" description="Review return requests and approve/reject/refund from one place." />

      <div className="panel">
        <div className="panel-toolbar">
          <input
            className="search-input"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search by order, product, reason, status..."
          />
          <p className="panel-toolbar-text">Showing {filteredReturns.length} requests</p>
        </div>

        {error ? <p className="form-error-banner">{error}</p> : null}

        {loading ? (
          <LoadingState label="Loading return requests..." />
        ) : filteredReturns.length === 0 ? (
          <p className="empty-text">No return requests found.</p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Return ID</th>
                  <th>Order</th>
                  <th>Product</th>
                  <th>Reason</th>
                  <th>Refund</th>
                  <th>Status</th>
                  <th>Created</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {filteredReturns.map((item) => (
                  <tr key={item.id}>
                    <td>#{item.id}</td>
                    <td>
                      #{item.orderId}
                      <p className="row-meta">User #{item.userId}</p>
                    </td>
                    <td>
                      <p>{item.itemTitle || "-"}</p>
                      <p className="row-meta">Product ID: {item.itemProductId || "-"}</p>
                    </td>
                    <td>
                      <p>{item.reason || "-"}</p>
                      {item.customerRemark ? <p className="row-meta">Customer: {item.customerRemark}</p> : null}
                      {item.adminRemark ? <p className="row-meta">Admin: {item.adminRemark}</p> : null}
                    </td>
                    <td>{formatCurrency(item.refundAmount)}</td>
                    <td>
                          <span className={`status-badge status-${normalizeReturnStatus(item.status)}`}>
                            {getDisplayStatus(item.status)}
                          </span>
                    </td>
                    <td>{formatDateTime(item.createdAt)}</td>
                    <td>
                      <div className="table-actions">
                        {STATUS_OPTIONS.map((status) => (
                          <button
                            key={status}
                            type="button"
                            className="btn btn-sm btn-outline"
                            disabled={savingId === item.id || normalizeReturnStatus(status) === normalizeReturnStatus(item.status)}
                            onClick={() => handleStatusChange(item, status)}
                          >
                            {savingId === item.id && status !== item.status ? "Saving..." : status}
                          </button>
                        ))}
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
