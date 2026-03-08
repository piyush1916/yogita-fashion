import React, { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import Input from "../components/common/Input";
import Button from "../components/common/Button";
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
    <div className="container-page py-10">
      <div className="mx-auto max-w-md rounded-2xl bg-white p-6 shadow-soft ring-1 ring-slate-200">
        <h1 className="text-2xl font-extrabold text-slate-900">Register</h1>
        <p className="mt-1 text-sm text-slate-600">Create your account to start shopping.</p>

        <form className="mt-6 space-y-4" onSubmit={onSubmit}>
          <Input
            label="Full Name"
            name="name"
            value={form.name}
            onChange={onChange}
            error={errors.name}
            placeholder="Your name"
            autoComplete="name"
          />

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
            placeholder="Create a password"
            autoComplete="new-password"
          />

          <Button disabled={loading} className="w-full">
            {loading ? "Creating..." : "Create Account"}
          </Button>
        </form>

        <div className="mt-5 text-sm text-slate-600">
          Already have an account?{" "}
          <Link className="font-semibold text-brand-700 hover:underline" to="/login">
            Login
          </Link>
        </div>
      </div>
    </div>
  );
}
