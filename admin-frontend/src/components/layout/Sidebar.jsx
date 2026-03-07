import { NavLink } from "react-router-dom";

const navItems = [
  { to: "/dashboard", label: "Dashboard", icon: "DB" },
  { to: "/products", label: "Products", icon: "PR" },
  { to: "/products/new", label: "Add Product", icon: "AD" },
  { to: "/orders", label: "Orders", icon: "OR" },
  { to: "/coupons", label: "Coupons", icon: "CP" },
  { to: "/returns", label: "Returns", icon: "RT" },
  { to: "/users", label: "Users", icon: "US" },
  { to: "/audit-logs", label: "Audit Logs", icon: "LG" },
];

export default function Sidebar({ mobileOpen, onNavigate }) {
  return (
    <aside className={`sidebar ${mobileOpen ? "open" : ""}`}>
      <div className="sidebar-brand">
        <p className="sidebar-brand-title">Yogita Fashion</p>
        <p className="sidebar-brand-subtitle">Admin Panel</p>
      </div>

      <nav className="sidebar-nav">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            onClick={onNavigate}
            className={({ isActive }) => `sidebar-link ${isActive ? "active" : ""}`}
          >
            <span className="sidebar-link-icon" aria-hidden="true">
              {item.icon}
            </span>
            <span>{item.label}</span>
          </NavLink>
        ))}
      </nav>
    </aside>
  );
}
