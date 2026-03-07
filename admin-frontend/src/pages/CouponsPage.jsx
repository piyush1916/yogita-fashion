import { useEffect, useMemo, useState } from "react";
import PageHeader from "../components/ui/PageHeader";
import LoadingState from "../components/ui/LoadingState";
import { createCoupon, deleteCoupon, getCoupons, updateCoupon } from "../services/couponService";
import { formatDateTime } from "../utils/formatters";

const EMPTY_FORM = {
  code: "",
  type: "percent",
  value: "",
  minOrderAmount: "",
  maxUses: "",
  maxUsesPerUser: "1",
  startAt: "",
  endAt: "",
  isActive: true,
};

function resolveApiErrorMessage(apiError, fallbackMessage) {
  const data = apiError?.response?.data;
  if (typeof data?.message === "string" && data.message.trim()) return data.message.trim();
  if (typeof data === "string" && data.trim()) return data.trim();
  if (typeof data?.title === "string" && data.title.trim()) return data.title.trim();

  const errors = data?.errors;
  if (errors && typeof errors === "object") {
    const firstList = Object.values(errors).find((value) => Array.isArray(value) && value.length > 0);
    if (firstList && typeof firstList[0] === "string") return firstList[0];
  }

  if (!apiError?.response) {
    return "Backend API not reachable. Please verify your Railway backend URL in VITE_API_BASE_URL.";
  }

  return fallbackMessage;
}

function toDateTimeLocalValue(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const pad = (number) => String(number).padStart(2, "0");
  const year = date.getFullYear();
  const month = pad(date.getMonth() + 1);
  const day = pad(date.getDate());
  const hours = pad(date.getHours());
  const minutes = pad(date.getMinutes());
  return `${year}-${month}-${day}T${hours}:${minutes}`;
}

export default function CouponsPage() {
  const [coupons, setCoupons] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState("");
  const [search, setSearch] = useState("");
  const [form, setForm] = useState(EMPTY_FORM);
  const [error, setError] = useState("");

  const loadCoupons = async () => {
    setLoading(true);
    setError("");
    try {
      const items = await getCoupons();
      setCoupons(items);
    } catch (apiError) {
      setError(resolveApiErrorMessage(apiError, "Failed to load coupons."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadCoupons();
  }, []);

  const filteredCoupons = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return coupons;
    return coupons.filter((coupon) =>
      [coupon.code, coupon.type, coupon.isActive ? "active" : "inactive"].join(" ").toLowerCase().includes(term)
    );
  }, [coupons, search]);

  const resetForm = () => {
    setForm(EMPTY_FORM);
    setEditingId("");
  };

  const handleChange = (event) => {
    const { name, value, type, checked } = event.target;
    setForm((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : value,
    }));
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError("");
    try {
      if (editingId) {
        await updateCoupon(editingId, form);
      } else {
        await createCoupon(form);
      }
      await loadCoupons();
      resetForm();
    } catch (apiError) {
      setError(resolveApiErrorMessage(apiError, "Failed to save coupon."));
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = (coupon) => {
    setEditingId(coupon.id);
    setForm({
      code: coupon.code,
      type: coupon.type,
      value: String(coupon.value ?? ""),
      minOrderAmount: String(coupon.minOrderAmount ?? ""),
      maxUses: String(coupon.maxUses ?? ""),
      maxUsesPerUser: String(coupon.maxUsesPerUser ?? "1"),
      startAt: toDateTimeLocalValue(coupon.startAt),
      endAt: toDateTimeLocalValue(coupon.endAt),
      isActive: Boolean(coupon.isActive),
    });
  };

  const handleDelete = async (coupon) => {
    const confirmed = window.confirm(`Delete coupon ${coupon.code}?`);
    if (!confirmed) return;

    try {
      await deleteCoupon(coupon.id);
      await loadCoupons();
      if (editingId === coupon.id) resetForm();
    } catch (apiError) {
      setError(resolveApiErrorMessage(apiError, "Failed to delete coupon."));
    }
  };

  return (
    <section>
      <PageHeader title="Coupons" description="Create and manage discount coupons with date and usage limits." />

      {error ? <p className="form-error-banner">{error}</p> : null}

      <form className="panel form-panel" onSubmit={handleSubmit}>
        <div className="form-grid">
          <label className="form-field">
            <span>Coupon Code *</span>
            <input name="code" value={form.code} onChange={handleChange} placeholder="SAVE10" required />
          </label>

          <label className="form-field">
            <span>Type *</span>
            <select name="type" value={form.type} onChange={handleChange}>
              <option value="percent">Percent</option>
              <option value="fixed">Fixed Amount</option>
            </select>
          </label>

          <label className="form-field">
            <span>{form.type === "percent" ? "Discount %" : "Discount Amount"} *</span>
            <input name="value" type="number" min="0" step="0.01" value={form.value} onChange={handleChange} required />
          </label>

          <label className="form-field">
            <span>Min Order Amount</span>
            <input
              name="minOrderAmount"
              type="number"
              min="0"
              step="0.01"
              value={form.minOrderAmount}
              onChange={handleChange}
            />
          </label>

          <label className="form-field">
            <span>Max Uses (Global)</span>
            <input name="maxUses" type="number" min="0" step="1" value={form.maxUses} onChange={handleChange} />
          </label>

          <label className="form-field">
            <span>Max Uses Per User</span>
            <input name="maxUsesPerUser" type="number" min="0" step="1" value={form.maxUsesPerUser} onChange={handleChange} />
          </label>

          <label className="form-field">
            <span>Start Date</span>
            <input name="startAt" type="datetime-local" value={form.startAt} onChange={handleChange} />
          </label>

          <label className="form-field">
            <span>End Date</span>
            <input name="endAt" type="datetime-local" value={form.endAt} onChange={handleChange} />
          </label>

          <label className="form-field form-checkbox">
            <input type="checkbox" name="isActive" checked={form.isActive} onChange={handleChange} />
            <span>Coupon is active</span>
          </label>
        </div>

        <div className="form-actions">
          {editingId ? (
            <button type="button" className="btn btn-outline" onClick={resetForm}>
              Cancel Edit
            </button>
          ) : null}
          <button type="submit" className="btn btn-primary" disabled={saving}>
            {saving ? "Saving..." : editingId ? "Update Coupon" : "Create Coupon"}
          </button>
        </div>
      </form>

      <div className="panel">
        <div className="panel-toolbar">
          <input
            className="search-input"
            placeholder="Search coupon code/type..."
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          <p className="panel-toolbar-text">Showing {filteredCoupons.length} coupons</p>
        </div>

        {loading ? (
          <LoadingState label="Loading coupons..." />
        ) : filteredCoupons.length === 0 ? (
          <p className="empty-text">No coupons found.</p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Type</th>
                  <th>Value</th>
                  <th>Usage</th>
                  <th>Active Dates</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredCoupons.map((coupon) => (
                  <tr key={coupon.id}>
                    <td>{coupon.code}</td>
                    <td>{coupon.type}</td>
                    <td>{coupon.type === "percent" ? `${coupon.value}%` : `Rs ${coupon.value}`}</td>
                    <td>
                      {coupon.usedCount}
                      {coupon.maxUses > 0 ? ` / ${coupon.maxUses}` : " / Unlimited"}
                      <p className="row-meta">Per user: {coupon.maxUsesPerUser > 0 ? coupon.maxUsesPerUser : "Unlimited"}</p>
                    </td>
                    <td>
                      <p>{coupon.startAt ? formatDateTime(coupon.startAt) : "Immediate"}</p>
                      <p className="row-meta">{coupon.endAt ? formatDateTime(coupon.endAt) : "No expiry"}</p>
                    </td>
                    <td>
                      <span className={`status-badge ${coupon.isActive ? "status-featured" : "status-neutral"}`}>
                        {coupon.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td>
                      <div className="table-actions">
                        <button type="button" className="btn btn-sm btn-outline" onClick={() => handleEdit(coupon)}>
                          Edit
                        </button>
                        <button type="button" className="btn btn-sm btn-danger" onClick={() => handleDelete(coupon)}>
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}
