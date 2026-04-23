import React, { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../hooks/useToast";
import { required, validateEmail } from "../utils/validators";

export default function Register() {
  const { register } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();

  const [form, setForm] = useState({ name: "", email: "", password: "" });
  const [errors, setErrors] = useState({});
  const [loading, setLoading] = useState(false);

  const onChange = (e) => {
    const { name, value } = e.target;
    setForm((p) => ({ ...p, [name]: value }));
  };

  const validate = () => {
    const next = {};

    if (!required(form.name)) next.name = "Name is required.";

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
      await register({
        name: form.name.trim(),
        email: form.email.trim(),
        password: form.password,
      });
      toast.success("Account created");
      navigate("/");
    } catch (err) {
      toast.error(err?.message || "Registration failed");
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
            <p className="auth-brand-copy">Create your account and unlock a smoother shopping journey.</p>
          </div>
        </aside>

        <div className="auth-divider-orb" aria-hidden="true">
          <span />
        </div>

        <div className="auth-card auth-card-register">
          <h1 className="auth-title">Register</h1>
          <p className="auth-subtitle">Create your account to start shopping.</p>

          <form className="auth-form" onSubmit={onSubmit} noValidate>
            <label className="auth-field">
              <span>Full Name</span>
              <input
                name="name"
                value={form.name}
                onChange={onChange}
                placeholder="Your name"
                autoComplete="name"
                className={errors.name ? "has-error" : ""}
              />
              {errors.name ? <small>{errors.name}</small> : null}
            </label>

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
                placeholder="Create a password"
                autoComplete="new-password"
                className={errors.password ? "has-error" : ""}
              />
              {errors.password ? <small>{errors.password}</small> : null}
            </label>

            <button type="submit" disabled={loading} className="auth-submit">
              {loading ? "Creating..." : "Create Account"}
            </button>
          </form>

          <div className="auth-switch-row">
            Already have an account?{" "}
            <Link className="auth-switch-link" to="/login">
              Login
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
