import { Outlet, useLocation } from "react-router-dom";
import { useEffect, useMemo, useState } from "react";
import Sidebar from "./Sidebar";
import HeaderBar from "./HeaderBar";

function getPageTitle(pathname) {
  if (pathname.startsWith("/dashboard")) return "Dashboard";
  if (pathname === "/products/new") return "Add Product";
  if (pathname.startsWith("/products/") && pathname.endsWith("/edit")) return "Edit Product";
  if (pathname.startsWith("/products")) return "Products";
  if (pathname.startsWith("/orders")) return "Orders";
  if (pathname.startsWith("/coupons")) return "Coupons";
  if (pathname.startsWith("/returns")) return "Returns";
  if (pathname.startsWith("/support-requests")) return "Support Requests";
  if (pathname.startsWith("/users")) return "Users";
  if (pathname.startsWith("/audit-logs")) return "Audit Logs";
  return "Admin Panel";
}

export default function AdminLayout() {
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  const pageTitle = useMemo(() => getPageTitle(location.pathname), [location.pathname]);

  return (
    <div className="app-shell">
      <Sidebar mobileOpen={mobileOpen} onNavigate={() => setMobileOpen(false)} />
      <button
        type="button"
        className={`sidebar-overlay ${mobileOpen ? "show" : ""}`}
        onClick={() => setMobileOpen(false)}
        aria-label="Close sidebar"
      />

      <div className="app-main">
        <HeaderBar title={pageTitle} onMenuToggle={() => setMobileOpen((prev) => !prev)} />
        <main className="page-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
