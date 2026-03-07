import { useEffect, useMemo, useState } from "react";
import PageHeader from "../components/ui/PageHeader";
import LoadingState from "../components/ui/LoadingState";
import { getAuditLogs } from "../services/auditLogService";
import { formatDateTime } from "../utils/formatters";

export default function AuditLogsPage() {
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");

  useEffect(() => {
    let ignore = false;
    const load = async () => {
      setLoading(true);
      setError("");
      try {
        const items = await getAuditLogs();
        if (!ignore) setLogs(items);
      } catch {
        if (!ignore) setError("Failed to load audit logs.");
      } finally {
        if (!ignore) setLoading(false);
      }
    };

    load();
    return () => {
      ignore = true;
    };
  }, []);

  const filteredLogs = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return logs;
    return logs.filter((log) =>
      [log.actorEmail, log.actorRole, log.action, log.entityType, log.entityId, log.details].join(" ").toLowerCase().includes(term)
    );
  }, [logs, search]);

  return (
    <section>
      <PageHeader title="Audit Logs" description="Track what admin users changed in the system." />

      <div className="panel">
        <div className="panel-toolbar">
          <input
            className="search-input"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search by actor, action, entity..."
          />
          <p className="panel-toolbar-text">Showing {filteredLogs.length} logs</p>
        </div>

        {error ? <p className="form-error-banner">{error}</p> : null}

        {loading ? (
          <LoadingState label="Loading audit logs..." />
        ) : filteredLogs.length === 0 ? (
          <p className="empty-text">No audit logs found.</p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>When</th>
                  <th>Actor</th>
                  <th>Action</th>
                  <th>Entity</th>
                  <th>Details</th>
                </tr>
              </thead>
              <tbody>
                {filteredLogs.map((log) => (
                  <tr key={log.id}>
                    <td>{formatDateTime(log.createdAt)}</td>
                    <td>
                      <p>{log.actorEmail || "Unknown"}</p>
                      <p className="row-meta">{log.actorRole || "-"}</p>
                    </td>
                    <td>{log.action}</td>
                    <td>
                      <p>{log.entityType}</p>
                      <p className="row-meta">ID: {log.entityId || "-"}</p>
                    </td>
                    <td>{log.details || "-"}</td>
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
