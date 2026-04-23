import { useEffect, useMemo, useState } from "react";
import PageHeader from "../components/ui/PageHeader";
import LoadingState from "../components/ui/LoadingState";
import { getSupportRequests } from "../services/supportService";
import { formatDateTime } from "../utils/formatters";

export default function SupportRequestsPage() {
  const [requests, setRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");

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
            <table className="table">
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
                    <td>#{item.id}</td>
                    <td>{item.subject || "-"}</td>
                    <td>
                      <p>{item.name || "-"}</p>
                      <p className="row-meta">{item.contact || item.email || item.phone || "-"}</p>
                    </td>
                    <td>{item.orderId || "-"}</td>
                    <td>
                      <span className={`status-badge status-${String(item.status || "").toLowerCase()}`}>{item.status}</span>
                    </td>
                    <td>{formatDateTime(item.createdAt)}</td>
                    <td>{item.message || "-"}</td>
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
