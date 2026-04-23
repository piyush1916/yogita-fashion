import React, { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../hooks/useToast";
import { required, validateEmail } from "../utils/validators";

export default function Login() {
  const { login } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();
  const location = useLocation();
  const from = location.state?.from || "/";

  const [form, setForm] = useState({ email: "", password: "" });
  const [errors, setErrors] = useState({});
  const [loading, setLoading] = useState(false);

  const onChange = (e) => {
    const { name, value } = e.target;
    setForm((p) => ({ ...p, [name]: value }));
  };

  const validate = () => {
    const next = {};

    if (!required(form.email)) next.email = "Email is required.";
    else if (!validateEmail(form.email)) next.email = "Enter a valid email.";

    if (!required(form.password)) next.password = "Password is required.";

    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const onSubmit = async (e) => {
    e.preventDefault();
    if (!validate()) {
      toast.error("Please fix the errors.");
      return;
    }

    try {
      setLoading(true);
      await login({ email: form.email.trim(), password: form.password });
      toast.success("Logged in successfully");
      navigate(from, { replace: true });
    } catch (err) {
      toast.error(err?.message || "Login failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <section className="auth-page">
      <div className="auth-shell">
        <aside className="auth-brand-panel" aria-hidden="true">
          <div className="auth-brand-content">
            <div className="auth-brand-mark" />
            <p className="auth-brand-name">Yogita Fashion</p>
            <p className="auth-brand-copy">Secure access for your personalized shopping experience.</p>
          </div>
        </aside>

        <div className="auth-divider-orb" aria-hidden="true">
          <span />
        </div>

        <div className="auth-card">
          <h1 className="auth-title">Login</h1>
          <p className="auth-subtitle">Welcome back! Login to continue.</p>

          <form className="auth-form" onSubmit={onSubmit} noValidate>
            <label className="auth-field">
              <span>Email</span>
              <input
                name="email"
                type="email"
                value={form.email}
                onChange={onChange}
                placeholder="you@example.com"
                autoComplete="email"
                className={errors.email ? "has-error" : ""}
              />
              {errors.email ? <small>{errors.email}</small> : null}
            </label>

            <label className="auth-field">
              <span>Password</span>
              <input
                name="password"
                type="password"
                value={form.password}
                onChange={onChange}
                placeholder="********"
                autoComplete="current-password"
                className={errors.password ? "has-error" : ""}
              />
              {errors.password ? <small>{errors.password}</small> : null}
            </label>

            <button type="submit" disabled={loading} className="auth-submit">
              {loading ? "Logging in..." : "Login"}
            </button>
          </form>

          <div className="auth-switch-row">
            Don&apos;t have an account?{" "}
            <Link className="auth-switch-link" to="/register">
              Register
            </Link>
          </div>

          <div className="auth-tip">
            <div className="auth-tip-title">Demo tip</div>
            <div>Register once, then use the same email/password to login.</div>
          </div>
        </div>
      </div>
    </section>
  );
}
