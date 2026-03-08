import React, { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import Input from "../components/common/Input";
import Button from "../components/common/Button";
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
    <div className="container-page py-10">
      <div className="mx-auto max-w-md rounded-2xl bg-white p-6 shadow-soft ring-1 ring-slate-200">
        <h1 className="text-2xl font-extrabold text-slate-900">Login</h1>
        <p className="mt-1 text-sm text-slate-600">Welcome back! Login to continue.</p>

        <form className="mt-6 space-y-4" onSubmit={onSubmit}>
          <Input
            label="Email"
            name="email"
            type="email"
            value={form.email}
            onChange={onChange}
            error={errors.email}
            placeholder="you@example.com"
            autoComplete="email"
          />

          <Input
            label="Password"
            name="password"
            type="password"
            value={form.password}
            onChange={onChange}
            error={errors.password}
            placeholder="••••••••"
            autoComplete="current-password"
          />

          <Button disabled={loading} className="w-full">
            {loading ? "Logging in..." : "Login"}
          </Button>
        </form>

        <div className="mt-5 text-sm text-slate-600">
          Don&apos;t have an account?{" "}
          <Link className="font-semibold text-brand-700 hover:underline" to="/register">
            Register
          </Link>
        </div>

        <div className="mt-6 rounded-xl bg-slate-50 p-3 text-xs text-slate-600">
          <div className="font-semibold">Demo tip</div>
          <div>Register once, then use the same email/password to login.</div>
        </div>
      </div>
    </div>
  );
}
