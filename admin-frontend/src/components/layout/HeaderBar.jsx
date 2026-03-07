import { useNavigate } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";

export default function HeaderBar({ title, onMenuToggle }) {
  const navigate = useNavigate();
  const { adminUser, logout } = useAuth();

  const currentDate = new Intl.DateTimeFormat("en-IN", {
    weekday: "short",
    day: "2-digit",
    month: "short",
    year: "numeric",
  }).format(new Date());

  const handleLogout = () => {
    logout();
    navigate("/login", { replace: true });
  };

  return (
    <header className="topbar">
      <div className="topbar-left">
        <button type="button" className="menu-toggle" onClick={onMenuToggle} aria-label="Open menu">
          <span />
          <span />
          <span />
        </button>
        <div>
          <p className="topbar-title">{title}</p>
          <p className="topbar-subtitle">{currentDate}</p>
        </div>
      </div>

      <div className="topbar-right">
        <div className="admin-chip">
          <p className="admin-chip-name">{adminUser?.name || "Admin"}</p>
          <p className="admin-chip-role">{adminUser?.role || "Admin"}</p>
        </div>
        <button type="button" className="btn btn-outline" onClick={handleLogout}>
          Logout
        </button>
      </div>
    </header>
  );
}
