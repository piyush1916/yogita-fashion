import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function NotFoundPage() {
  const { isAuthenticated } = useAuth();

  return (
    <div className="not-found-page">
      <div className="panel not-found-card">
        <p className="not-found-code">404</p>
        <h1>Page Not Found</h1>
        <p>The page you are looking for does not exist.</p>
        <Link className="btn btn-primary" to={isAuthenticated ? "/dashboard" : "/login"}>
          {isAuthenticated ? "Go to Dashboard" : "Go to Login"}
        </Link>
      </div>
    </div>
  );
}
