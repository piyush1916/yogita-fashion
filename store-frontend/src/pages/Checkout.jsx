import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useCart } from "../context/CartContext";
import { useToast } from "../hooks/useToast";
import ordersService from "../services/ordersService";
import { validateCheckoutForm } from "../utils/validators";

const normalizeIssueProductId = (value) => {
  const raw = String(value ?? "").trim();
  if (!raw) return "";
  const head = raw.split("__")[0].trim();
  const digits = head.match(/\d+/)?.[0] || raw.match(/\d+/)?.[0];
  return digits || head;
};

const Checkout = () => {
  const { user } = useAuth();
  const { items, clearCart, subtotal, removeFromCart, updateQty } = useCart();
  const navigate = useNavigate();
  const toast = useToast();
  const [form, setForm] = useState({
    name: user?.name || "",
    phone: user?.phone || "",
    email: user?.email || "",
    address: "",
    city: user?.city || "",
    pincode: "",
    payment: "COD",
  });
  const [errors, setErrors] = useState({});

  const handleChange = (e) => setForm({ ...form, [e.target.name]: e.target.value });

  const reconcileCartFromOrderIssues = (issues) => {
    if (!Array.isArray(issues) || issues.length === 0) return false;

    const issuesByProductId = new Map();
    for (const issue of issues) {
      const productId = normalizeIssueProductId(issue?.productId);
      if (productId) {
        issuesByProductId.set(productId, issue);
      }
    }

    if (issuesByProductId.size === 0) return false;

    const itemsByProductId = new Map();
    for (const item of items) {
      const productId = normalizeIssueProductId(item?.productId ?? item?.id);
      if (!productId) continue;
      const bucket = itemsByProductId.get(productId) ?? [];
      bucket.push(item);
      itemsByProductId.set(productId, bucket);
    }

    let changed = false;
    for (const [productId, issue] of issuesByProductId.entries()) {
      const bucket = itemsByProductId.get(productId) ?? [];
      if (bucket.length === 0) continue;

      const message = String(issue?.message ?? "").toLowerCase();
      const available = Number(issue?.available);
      const mustRemoveAll =
        message.includes("invalid product id") || message.includes("product not found") || (Number.isFinite(available) && available <= 0);

      if (mustRemoveAll) {
        for (const line of bucket) {
          removeFromCart(line.key);
          changed = true;
        }
        continue;
      }

      if (!Number.isFinite(available)) continue;

      let remaining = Math.max(0, Math.floor(available));
      for (const line of bucket) {
        const currentQty = Math.max(1, Number(line?.qty) || 1);
        if (remaining <= 0) {
          removeFromCart(line.key);
          changed = true;
          continue;
        }

        if (currentQty > remaining) {
          updateQty(line.key, remaining);
          changed = true;
          remaining = 0;
          continue;
        }

        remaining -= currentQty;
      }
    }

    return changed;
  };

  const placeOrder = async () => {
    if (items.length === 0) {
      toast.error("Your cart is empty. Add product before placing order.");
      navigate("/shop");
      return;
    }

    const errs = validateCheckoutForm(form);
    if (Object.keys(errs).length) {
      setErrors(errs);
      toast.error("Fix form errors");
      return;
    }
    try {
      const order = await ordersService.createOrder({
        ...form,
        userId: Number(user?.id) || 0,
        items,
        total: subtotal,
      });
      clearCart();
      navigate("/track-order", {
        state: { orderId: order.orderNumber || order.id, contact: form.email.trim() || form.phone.trim() },
      });
    } catch (error) {
      const adjusted = reconcileCartFromOrderIssues(error?.issues);
      if (adjusted) {
        toast.error(error?.message || "Some cart items are unavailable. Cart updated, please review and try again.");
        return;
      }

      toast.error(error?.message || "Unable to place order right now.");
    }
  };

  return (
    <div className="container mx-auto px-4 py-10">
      <h1 className="text-2xl font-bold mb-4">Checkout</h1>
      <div className="max-w-lg space-y-4">
        <div>
          <label>Name</label>
          <input name="name" value={form.name} onChange={handleChange} className="w-full border px-2 py-1 rounded" />
          {errors.name && <p className="text-red-600 text-sm">{errors.name}</p>}
        </div>
        <div>
          <label>Phone</label>
          <input name="phone" value={form.phone} onChange={handleChange} className="w-full border px-2 py-1 rounded" />
          {errors.phone && <p className="text-red-600 text-sm">{errors.phone}</p>}
        </div>
        <div>
          <label>Email</label>
          <input name="email" value={form.email} onChange={handleChange} className="w-full border px-2 py-1 rounded" />
          {errors.email && <p className="text-red-600 text-sm">{errors.email}</p>}
        </div>
        <div>
          <label>Address</label>
          <input name="address" value={form.address} onChange={handleChange} className="w-full border px-2 py-1 rounded" />
          {errors.address && <p className="text-red-600 text-sm">{errors.address}</p>}
        </div>
        <div>
          <label>City</label>
          <input name="city" value={form.city} onChange={handleChange} className="w-full border px-2 py-1 rounded" />
          {errors.city && <p className="text-red-600 text-sm">{errors.city}</p>}
        </div>
        <div>
          <label>Pincode</label>
          <input name="pincode" value={form.pincode} onChange={handleChange} className="w-full border px-2 py-1 rounded" />
          {errors.pincode && <p className="text-red-600 text-sm">{errors.pincode}</p>}
        </div>
        <div>
          <h3 className="font-semibold">Payment</h3>
          <select name="payment" value={form.payment} onChange={handleChange} className="w-full border px-2 py-1 rounded">
            <option value="COD">Cash on Delivery</option>
            <option value="ONLINE">Online (placeholder)</option>
          </select>
        </div>
        <button onClick={placeOrder} className="bg-indigo-600 text-white px-4 py-2 rounded">
          Place Order
        </button>
      </div>
    </div>
  );
};

export default Checkout;
