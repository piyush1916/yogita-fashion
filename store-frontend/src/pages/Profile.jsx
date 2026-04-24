import React, { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../hooks/useToast";
import { required, validateEmail, validatePhone } from "../utils/validators";

const emptyForm = {
  name: "",
  email: "",
  phone: "",
  city: "",
};

const formatJoinedAt = (value) => {
  const timestamp = Date.parse(String(value || ""));
  if (!Number.isFinite(timestamp)) return "N/A";
  return new Date(timestamp).toLocaleDateString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
};

export default function Profile() {
  const { user, updateProfile, logout } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();

  const [form, setForm] = useState(emptyForm);
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!user) return;
    setForm({
      name: user.name || "",
      email: user.email || "",
      phone: user.phone || "",
      city: user.city || "",
    });
  }, [user]);

  const initials = useMemo(() => {
    const source = user?.name || user?.email || "YF";
    const parts = source.split(" ").filter(Boolean);
    return parts
      .slice(0, 2)
      .map((p) => p[0]?.toUpperCase() || "")
      .join("");
  }, [user]);

  const validate = () => {
    const next = {};
    if (!required(form.name)) next.name = "Name is required.";
    if (!required(form.email)) next.email = "Email is required.";
    else if (!validateEmail(form.email)) next.email = "Enter a valid email.";
    if (required(form.phone) && !validatePhone(form.phone)) {
      next.phone = "Enter a valid 10-digit phone.";
    }
    if (!required(form.city)) next.city = "City is required.";

    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const onChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: "" }));
    }
  };

  const onSubmit = async (e) => {
    e.preventDefault();
    if (!validate()) {
      toast.error("Please fix the highlighted fields.");
      return;
    }

    try {
      setSaving(true);
      await updateProfile({
        name: form.name.trim(),
        email: form.email.trim(),
        phone: form.phone.trim(),
        city: form.city.trim(),
      });
      toast.success("Profile updated successfully.");
    } catch (err) {
      toast.error(err?.message || "Could not update profile.");
    } finally {
      setSaving(false);
    }
  };

  const onLogout = () => {
    logout();
    toast.info("Logged out.");
    navigate("/");
  };

  if (!user) {
    return (
      <section className="profilePage">
        <div className="container profileWrap profileWrapSingle">
          <div className="profileCard profileEmpty">
            <h1 className="profileTitle">My Profile</h1>
            <p className="profileSubtext">Please login to view and update your profile.</p>
            <div className="profileActions">
              <Link to="/login" className="profileBtn profileBtnPrimary">
                Login
              </Link>
              <Link to="/register" className="profileBtn profileBtnGhost">
                Create Account
              </Link>
            </div>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section className="profilePage">
      <div className="container profileWrap">
        <aside className="profileCard profileSidebar">
          <div className="profileAvatar">{initials || "YF"}</div>
          <h1 className="profileTitle">My Profile</h1>
          <p className="profileSubtext">Manage your account details and contact information.</p>

          <div className="profileMeta">
            <div className="profileMetaRow">
              <span>Member ID</span>
              <strong>{String(user.id || "-").slice(-8)}</strong>
            </div>
            <div className="profileMetaRow">
              <span>Joined</span>
              <strong>{formatJoinedAt(user.createdAt)}</strong>
            </div>
          </div>

          <div className="profileActions">
            <Link to="/orders" className="profileBtn profileBtnGhost">
              My Orders
            </Link>
            <Link to="/track-order" className="profileBtn profileBtnGhost">
              Track Order
            </Link>
            <button type="button" className="profileBtn profileBtnDanger" onClick={onLogout}>
              Logout
            </button>
          </div>
        </aside>

        <div className="profileCard profileMain">
          <h2 className="profileSectionTitle">Personal Information</h2>
          <p className="profileSubtext">Keep your details up to date for smooth checkout and support.</p>

          <form className="profileForm" onSubmit={onSubmit}>
            <label className="profileField">
              <span>Full Name</span>
              <input name="name" value={form.name} onChange={onChange} placeholder="Your full name" />
              {errors.name && <small>{errors.name}</small>}
            </label>

            <label className="profileField">
              <span>Email</span>
              <input name="email" value={form.email} onChange={onChange} placeholder="you@example.com" />
              {errors.email && <small>{errors.email}</small>}
            </label>

            <label className="profileField">
              <span>Phone</span>
              <input name="phone" value={form.phone} onChange={onChange} placeholder="10-digit mobile number" />
              {errors.phone && <small>{errors.phone}</small>}
            </label>

            <label className="profileField">
              <span>City</span>
              <input name="city" value={form.city} onChange={onChange} placeholder="Your city" />
              {errors.city && <small>{errors.city}</small>}
            </label>

            <div className="profileFormActions">
              <button type="submit" className="profileBtn profileBtnPrimary" disabled={saving}>
                {saving ? "Saving..." : "Save Changes"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </section>
  );
}
