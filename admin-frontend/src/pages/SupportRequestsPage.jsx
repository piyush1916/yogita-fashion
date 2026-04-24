import { useEffect, useMemo, useState } from "react";
import PageHeader from "../components/ui/PageHeader";
import LoadingState from "../components/ui/LoadingState";
import { getSupportRequests, updateSupportRequestStatus } from "../services/supportService";
import { formatDateTime } from "../utils/formatters";

const SUPPORT_STATUS_OPTIONS = ["Open", "Pending", "In Progress", "Resolved", "Closed"];

export default function SupportRequestsPage() {
  const [requests, setRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [updatingId, setUpdatingId] = useState("");

  useEffect(() => {
    let ignore = false;

    const loadRequests = async () => {
      setLoading(true);
      setError("");
      try {
        const items = await getSupportRequests();
        if (!ignore) {
          setRequests(items);
        }
      } catch {
        if (!ignore) {
          setError("Failed to load support requests.");
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    };

    loadRequests();
    return () => {
      ignore = true;
    };
  }, []);

  const filteredRequests = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return requests;
    return requests.filter((item) =>
      [
        item.id,
        item.subject,
        item.message,
        item.orderId,
        item.name,
        item.contact,
        item.email,
        item.phone,
        item.status,
      ]
        .join(" ")
        .toLowerCase()
        .includes(term)
    );
  }, [requests, search]);

  const handleStatusChange = async (requestId, nextStatus) => {
    setUpdatingId(requestId);
    setError("");

    try {
      const updatedItem = await updateSupportRequestStatus(requestId, nextStatus);
      if (!updatedItem) {
        throw new Error("Invalid support API response.");
      }

      setRequests((prev) => prev.map((item) => (item.id === requestId ? updatedItem : item)));
    } catch {
      setError("Failed to update support request status.");
    } finally {
      setUpdatingId("");
    }
  };

  return (
    <section>
      <PageHeader
        title="Support Requests"
        description="View customer support tickets raised from the store support page."
      />

      <div className="panel">
        <div className="panel-toolbar">
          <input
            className="search-input"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search by subject, message, order, customer..."
          />
          <p className="panel-toolbar-text">Showing {filteredRequests.length} requests</p>
        </div>

        {error ? <p className="form-error-banner">{error}</p> : null}

        {loading ? (
          <LoadingState label="Loading support requests..." />
        ) : filteredRequests.length === 0 ? (
          <p className="empty-text">No support requests found.</p>
        ) : (
          <div className="table-wrap">
            <table className="table table-mobile-stack">
              <thead>
                <tr>
                  <th>Ticket</th>
                  <th>Subject</th>
                  <th>Customer</th>
                  <th>Order</th>
                  <th>Status</th>
                  <th>Created</th>
                  <th>Message</th>
                </tr>
              </thead>
              <tbody>
                {filteredRequests.map((item) => (
                  <tr key={item.id}>
                    <td data-label="Ticket">#{item.id}</td>
                    <td data-label="Subject">{item.subject || "-"}</td>
                    <td data-label="Customer">
                      <p>{item.name || "-"}</p>
                      <p className="row-meta">{item.contact || item.email || item.phone || "-"}</p>
                    </td>
                    <td data-label="Order">{item.orderId || "-"}</td>
                    <td data-label="Status">
                      <div className="table-actions">
                        <span className={`status-badge status-${String(item.status || "").toLowerCase().replace(/\s+/g, "-")}`}>
                          {item.status}
                        </span>
                        <select
                          className="table-select"
                          value={item.status}
                          onChange={(event) => handleStatusChange(item.id, event.target.value)}
                          disabled={updatingId === item.id}
                        >
                          {SUPPORT_STATUS_OPTIONS.map((status) => (
                            <option key={status} value={status}>
                              {status}
                            </option>
                          ))}
                        </select>
                      </div>
                    </td>
                    <td data-label="Created">{formatDateTime(item.createdAt)}</td>
                    <td data-label="Message">{item.message || "-"}</td>
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
