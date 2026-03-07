import { useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

function resolveErrorMessage(error) {
  if (typeof error?.response?.data === "string") return error.response.data;
  return error?.message || "Unable to login. Please check your credentials.";
}

export default function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [form, setForm] = useState({ email: "", password: "" });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />;
  }

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    setError("");
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError("");

    try {
      await login(form);
      const redirectTo = location.state?.from?.pathname || "/dashboard";
      navigate(redirectTo, { replace: true });
    } catch (loginError) {
      setError(resolveErrorMessage(loginError));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-bg-shape shape-1" />
      <div className="login-bg-shape shape-2" />

      <form className="login-card" onSubmit={handleSubmit}>
        <p className="login-kicker">Yogita Fashion</p>
        <h1>Admin Login</h1>
        <p className="login-subtitle">Manage products, orders, users, and store insights from one place.</p>

        <label className="form-field">
          <span>Email</span>
          <input
            type="email"
            name="email"
            value={form.email}
            onChange={handleChange}
            placeholder="admin@yogitafashion.com"
            required
          />
        </label>

        <label className="form-field">
          <span>Password</span>
          <input
            type="password"
            name="password"
            value={form.password}
            onChange={handleChange}
            placeholder="Enter password"
            required
          />
        </label>

        {error ? <p className="form-error-banner">{error}</p> : null}

        <button type="submit" className="btn btn-primary btn-block" disabled={submitting}>
          {submitting ? "Logging in..." : "Login to Admin Panel"}
        </button>
      </form>
    </div>
  );
}
