import React, { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../hooks/useToast";
import addressService from "../services/addressService";
import { required, validatePhone, validatePincode } from "../utils/validators";

const emptyForm = {
  fullName: "",
  phone: "",
  line1: "",
  line2: "",
  city: "",
  state: "",
  pincode: "",
  landmark: "",
  type: "Home",
  isDefault: false,
};

function normalizeAddress(form) {
  return {
    fullName: String(form.fullName || "").trim(),
    phone: String(form.phone || "").trim(),
    line1: String(form.line1 || "").trim(),
    line2: String(form.line2 || "").trim(),
    city: String(form.city || "").trim(),
    state: String(form.state || "").trim(),
    pincode: String(form.pincode || "").trim(),
    landmark: String(form.landmark || "").trim(),
    type: String(form.type || "Home").trim() || "Home",
    isDefault: Boolean(form.isDefault),
  };
}

function validateAddress(form) {
  const errors = {};
  if (!required(form.fullName)) errors.fullName = "Full name is required.";
  if (!required(form.phone)) errors.phone = "Phone is required.";
  else if (!validatePhone(form.phone)) errors.phone = "Enter a valid 10-digit phone.";
  if (!required(form.line1)) errors.line1 = "Address line is required.";
  if (!required(form.city)) errors.city = "City is required.";
  if (!required(form.state)) errors.state = "State is required.";
  if (!required(form.pincode)) errors.pincode = "Pincode is required.";
  else if (!validatePincode(form.pincode)) errors.pincode = "Enter a valid 6-digit pincode.";
  return errors;
}

function formatAddress(address) {
  return [address.line1, address.line2, address.landmark, address.city, address.state, address.pincode]
    .filter(Boolean)
    .join(", ");
}

export default function SavedAddress() {
  const { user } = useAuth();
  const toast = useToast();
  const [addresses, setAddresses] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [errors, setErrors] = useState({});
  const [editingId, setEditingId] = useState("");

  useEffect(() => {
    if (!user) return;
    setAddresses(addressService.listByUser(user));
  }, [user]);

  const visibleAddresses = useMemo(() => {
    return [...addresses].sort((a, b) => Number(Boolean(b.isDefault)) - Number(Boolean(a.isDefault)));
  }, [addresses]);

  const persist = (nextAddresses) => {
    setAddresses(nextAddresses);
    if (user) {
      addressService.saveByUser(user, nextAddresses);
    }
  };

  const onChange = (e) => {
    const { name, type, checked, value } = e.target;
    setForm((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : value,
    }));

    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: "" }));
    }
  };

  const resetForm = () => {
    setForm(emptyForm);
    setErrors({});
    setEditingId("");
  };

  const onSubmit = (e) => {
    e.preventDefault();
    const nextErrors = validateAddress(form);
    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      toast.error("Please fix the highlighted address fields.");
      return;
    }

    const payload = normalizeAddress(form);
    const now = new Date().toISOString();
    const forceDefault = addresses.length === 0;

    if (editingId) {
      const next = addresses.map((address) => {
        if (address.id !== editingId) {
          return payload.isDefault || forceDefault ? { ...address, isDefault: false } : address;
        }
        return {
          ...address,
          ...payload,
          isDefault: payload.isDefault || forceDefault,
          updatedAt: now,
        };
      });
      persist(next);
      toast.success("Address updated.");
      resetForm();
      return;
    }

    const newAddress = {
      id: `${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
      ...payload,
      isDefault: payload.isDefault || forceDefault,
      createdAt: now,
      updatedAt: now,
    };

    const next = (payload.isDefault || forceDefault ? addresses.map((address) => ({ ...address, isDefault: false })) : addresses)
      .concat(newAddress);
    persist(next);
    toast.success("Address saved.");
    resetForm();
  };

  const onEdit = (address) => {
    setEditingId(address.id);
    setErrors({});
    setForm({
      fullName: address.fullName || "",
      phone: address.phone || "",
      line1: address.line1 || "",
      line2: address.line2 || "",
      city: address.city || "",
      state: address.state || "",
      pincode: address.pincode || "",
      landmark: address.landmark || "",
      type: address.type || "Home",
      isDefault: Boolean(address.isDefault),
    });
  };

  const onDelete = (id) => {
    const addressToDelete = addresses.find((address) => address.id === id);
    const remaining = addresses.filter((address) => address.id !== id);
    if (addressToDelete?.isDefault && remaining.length > 0) {
      remaining[0] = { ...remaining[0], isDefault: true };
    }
    persist(remaining);
    if (editingId === id) {
      resetForm();
    }
    toast.info("Address removed.");
  };

  const onSetDefault = (id) => {
    const next = addresses.map((address) => ({
      ...address,
      isDefault: address.id === id,
    }));
    persist(next);
    if (editingId === id) {
      setForm((prev) => ({ ...prev, isDefault: true }));
    }
    toast.success("Default address updated.");
  };

  if (!user) {
    return (
      <section className="profilePage">
        <div className="container profileWrap profileWrapSingle">
          <div className="profileCard profileEmpty">
            <h1 className="profileTitle">Saved Address</h1>
            <p className="profileSubtext">Please login to manage your saved addresses.</p>
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
    <section className="ordersPage">
      <div className="container ordersWrap">
        <p className="ordersBreadcrumb">
          <Link to="/profile">Your Account</Link>
          <span aria-hidden="true">{">"}</span>
          <span>Saved Address</span>
        </p>

        <div className="ordersHeader">
          <h1 className="ordersTitle">Saved Address</h1>
        </div>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <div className="profileCard">
            <h2 className="profileSectionTitle">{editingId ? "Edit Address" : "Add New Address"}</h2>
            <p className="profileSubtext">Save your delivery address for faster checkout.</p>

            <form className="profileForm mt-4" onSubmit={onSubmit}>
              <label className="profileField">
                <span>Full Name</span>
                <input name="fullName" value={form.fullName} onChange={onChange} placeholder="Full name" />
                {errors.fullName && <small>{errors.fullName}</small>}
              </label>

              <label className="profileField">
                <span>Phone</span>
                <input name="phone" value={form.phone} onChange={onChange} placeholder="10-digit mobile number" />
                {errors.phone && <small>{errors.phone}</small>}
              </label>

              <label className="profileField">
                <span>Address Line 1</span>
                <input name="line1" value={form.line1} onChange={onChange} placeholder="Flat/House, Street" />
                {errors.line1 && <small>{errors.line1}</small>}
              </label>

              <label className="profileField">
                <span>Address Line 2</span>
                <input name="line2" value={form.line2} onChange={onChange} placeholder="Area (optional)" />
              </label>

              <label className="profileField">
                <span>City</span>
                <input name="city" value={form.city} onChange={onChange} placeholder="City" />
                {errors.city && <small>{errors.city}</small>}
              </label>

              <label className="profileField">
                <span>State</span>
                <input name="state" value={form.state} onChange={onChange} placeholder="State" />
                {errors.state && <small>{errors.state}</small>}
              </label>

              <label className="profileField">
                <span>Pincode</span>
                <input name="pincode" value={form.pincode} onChange={onChange} placeholder="6-digit pincode" />
                {errors.pincode && <small>{errors.pincode}</small>}
              </label>

              <label className="profileField">
                <span>Landmark</span>
                <input name="landmark" value={form.landmark} onChange={onChange} placeholder="Landmark (optional)" />
              </label>

              <label className="profileField">
                <span>Address Type</span>
                <select className="ordersRange w-full" name="type" value={form.type} onChange={onChange}>
                  <option value="Home">Home</option>
                  <option value="Work">Work</option>
                  <option value="Other">Other</option>
                </select>
              </label>

              <label className="profileField">
                <span>Default</span>
                <div className="inline-flex items-center gap-2 text-sm text-[color:var(--muted)]">
                  <input type="checkbox" name="isDefault" checked={form.isDefault} onChange={onChange} />
                  Set as default address
                </div>
              </label>

              <div className="profileFormActions flex flex-wrap gap-2">
                <button type="submit" className="profileBtn profileBtnPrimary">
                  {editingId ? "Update Address" : "Save Address"}
                </button>
                {editingId && (
                  <button type="button" className="profileBtn profileBtnGhost" onClick={resetForm}>
                    Cancel Edit
                  </button>
                )}
              </div>
            </form>
          </div>

          <div className="profileCard">
            <h2 className="profileSectionTitle">Your Saved Addresses</h2>
            <p className="profileSubtext">
              {visibleAddresses.length} saved address{visibleAddresses.length === 1 ? "" : "es"}.
            </p>

            {visibleAddresses.length === 0 ? (
              <div className="ordersPlaceholder mt-4">No saved address yet. Add one using the form.</div>
            ) : (
              <div className="mt-4 grid gap-3">
                {visibleAddresses.map((address) => (
                  <article
                    key={address.id}
                    className="rounded-xl border border-white/20 bg-white/5 p-4 shadow-[0_12px_28px_rgba(0,0,0,0.25)]"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <h3 className="text-lg font-semibold">{address.fullName}</h3>
                      <div className="flex items-center gap-2">
                        <span className="rounded-full border border-white/25 px-3 py-1 text-xs font-semibold text-[color:var(--muted)]">
                          {address.type || "Home"}
                        </span>
                        {address.isDefault && (
                          <span className="rounded-full bg-emerald-500/20 px-3 py-1 text-xs font-semibold text-emerald-200">
                            Default
                          </span>
                        )}
                      </div>
                    </div>

                    <p className="mt-2 text-sm text-[color:var(--muted)]">{formatAddress(address)}</p>
                    <p className="mt-1 text-sm text-[color:var(--muted)]">Phone: {address.phone}</p>

                    <div className="profileActions mt-4">
                      {!address.isDefault && (
                        <button type="button" className="profileBtn profileBtnGhost" onClick={() => onSetDefault(address.id)}>
                          Set Default
                        </button>
                      )}
                      <button type="button" className="profileBtn profileBtnGhost" onClick={() => onEdit(address)}>
                        Edit
                      </button>
                      <button type="button" className="profileBtn profileBtnDanger" onClick={() => onDelete(address.id)}>
                        Delete
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
