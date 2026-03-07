import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function ProtectedRoute() {
  const { isAuthenticated, adminUser } = useAuth();
  const location = useLocation();

  if (!isAuthenticated || String(adminUser?.role || "").toLowerCase() !== "admin") {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
}
