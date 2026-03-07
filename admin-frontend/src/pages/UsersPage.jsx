import { useEffect, useMemo, useState } from "react";
import PageHeader from "../components/ui/PageHeader";
import LoadingState from "../components/ui/LoadingState";
import { getUsers } from "../services/userService";
import { formatDateTime } from "../utils/formatters";

export default function UsersPage() {
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");

  useEffect(() => {
    let ignore = false;

    const loadUsers = async () => {
      setLoading(true);
      setError("");
      try {
        const data = await getUsers();
        if (!ignore) {
          setUsers(data);
        }
      } catch {
        if (!ignore) {
          setError("Failed to load users.");
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    };

    loadUsers();
    return () => {
      ignore = true;
    };
  }, []);

  const filteredUsers = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return users;

    return users.filter((user) =>
      [user.name, user.email, user.phone, user.city, user.role]
        .join(" ")
        .toLowerCase()
        .includes(term)
    );
  }, [users, search]);

  return (
    <section>
      <PageHeader title="Users" description="View registered users and basic profile information." />

      <div className="panel">
        <div className="panel-toolbar">
          <input
            className="search-input"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search by name, email, phone, city..."
          />
          <p className="panel-toolbar-text">Showing {filteredUsers.length} users</p>
        </div>

        {error ? <p className="form-error-banner">{error}</p> : null}

        {loading ? (
          <LoadingState label="Loading users..." />
        ) : filteredUsers.length === 0 ? (
          <p className="empty-text">No users found.</p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>User</th>
                  <th>Email</th>
                  <th>Phone</th>
                  <th>City</th>
                  <th>Role</th>
                  <th>Created At</th>
                </tr>
              </thead>
              <tbody>
                {filteredUsers.map((user) => (
                  <tr key={user.id}>
                    <td>{user.name || "-"}</td>
                    <td>{user.email || "-"}</td>
                    <td>{user.phone || "-"}</td>
                    <td>{user.city || "-"}</td>
                    <td>
                      <span className={`status-badge ${user.role.toLowerCase() === "admin" ? "status-featured" : "status-neutral"}`}>
                        {user.role}
                      </span>
                    </td>
                    <td>{formatDateTime(user.createdAt)}</td>
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
