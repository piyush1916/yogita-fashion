import React, { useEffect, useMemo, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import ordersService from "../services/ordersService";
import returnsService from "../services/returnsService";
import { validateTrackOrderForm } from "../utils/validators";
import { useToast } from "../hooks/useToast";
import { ORDER_STATUSES } from "../utils/constants";
import { formatCurrency } from "../utils/currency";

function formatOrderDate(value) {
  const ts = Date.parse(value || "");
  if (!Number.isFinite(ts)) return "N/A";
  return new Date(ts).toLocaleString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

const RETURN_TRACKING_STEPS = ["Pending", "Pickup Started", "Pickup Completed", "Refunded"];

const normalizeReturnStatus = (status) => String(status || "").trim().toLowerCase().replace(/[\s_-]+/g, "");

const getReturnDisplayStatus = (status) => {
  const normalized = normalizeReturnStatus(status);
  if (!normalized) return "";
  if (normalized === "approved" || normalized === "pickupstarted") return "Pickup Started";
  if (normalized === "completed" || normalized === "returned" || normalized === "pickupcompleted") {
    return "Pickup Completed";
  }
  if (normalized === "pending") return "Pending";
  if (normalized === "refunded") return "Refunded";
  if (normalized === "rejected") return "Rejected";
  return String(status || "").trim();
};

const getReturnTrackingIndex = (status) => {
  const normalized = normalizeReturnStatus(status);
  if (!normalized) return -1;
  if (normalized === "pending") return 0;
  if (normalized === "approved" || normalized === "pickupstarted") return 1;
  if (normalized === "completed" || normalized === "returned" || normalized === "pickupcompleted") return 2;
  if (normalized === "refunded") return 3;
  return -1;
};

export default function TrackOrder() {
  const loc = useLocation();
  const { user } = useAuth();
  const toast = useToast();

  const [form, setForm] = useState({
    orderId: String(loc.state?.orderId || "").trim(),
    contact: String(loc.state?.contact || "").trim(),
  });
  const [errors, setErrors] = useState({});
  const [order, setOrder] = useState(null);
  const [returnRequests, setReturnRequests] = useState([]);
  const [tracking, setTracking] = useState(false);

  useEffect(() => {
    if (!user) return;
    setForm((prev) => ({
      ...prev,
      contact: prev.contact || user.email || user.phone || "",
    }));
  }, [user]);

  const items = useMemo(() => {
    return Array.isArray(order?.items) ? order.items : [];
  }, [order]);

  const itemCount = useMemo(() => {
    return items.reduce((sum, item) => sum + (Number(item?.qty) || 0), 0);
  }, [items]);

  const orderTotal = useMemo(() => {
    const totalFromOrder = Number(order?.total);
    if (Number.isFinite(totalFromOrder) && totalFromOrder > 0) return totalFromOrder;
    return items.reduce((sum, item) => {
      const qty = Number(item?.qty) || 0;
      const price = Number(item?.price) || 0;
      return sum + qty * price;
    }, 0);
  }, [order, items]);

  const statusIndex = useMemo(() => {
    if (!order?.status) return -1;
    return ORDER_STATUSES.indexOf(order.status);
  }, [order]);

  const shareText = useMemo(() => {
    if (!order) return "";
    return [
      "Yogita Fashion - Order Tracking",
      `Order ID: ${order.orderNumber || order.id || "N/A"}`,
      `Tracking No: ${order.trackingNumber || "N/A"}`,
      `Status: ${order.status || "Pending"}`,
      `Items: ${itemCount}`,
      `Total: ${formatCurrency(orderTotal)}`,
    ].join("\n");
  }, [order, itemCount, orderTotal]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: "" }));
    }
  };

  const onTrack = async (e) => {
    e.preventDefault();
    const payload = {
      orderId: String(form.orderId || "").trim(),
      contact: String(form.contact || "").trim(),
    };

    const errs = validateTrackOrderForm(payload);
    if (Object.keys(errs).length > 0) {
      setErrors(errs);
      toast.error("Please fix form errors.");
      return;
    }

    try {
      setTracking(true);
      const found = await ordersService.trackOrder(payload);
      if (!found) {
        setOrder(null);
        toast.error("Order not found. Check Order ID and contact.");
        return;
      }
      setOrder(found);
    } catch (error) {
      const message =
        typeof error?.response?.data?.message === "string"
          ? error.response.data.message
          : typeof error?.response?.data === "string"
          ? error.response.data
          : "Could not track order right now.";
      toast.error(message);
    } finally {
      setTracking(false);
    }
  };

  const onShare = async () => {
    if (!order || !shareText) return;

    try {
      if (typeof navigator !== "undefined" && navigator.share) {
        await navigator.share({
          title: "Order Tracking",
          text: shareText,
        });
        toast.success("Tracking details shared.");
        return;
      }
    } catch (err) {
      if (err?.name === "AbortError") return;
    }

    try {
      if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(shareText);
        toast.success("Tracking details copied.");
        return;
      }
    } catch {
      // fall through
    }

    toast.info("Share not supported on this device.");
  };

  useEffect(() => {
    if (!order || !user) {
      setReturnRequests([]);
      return;
    }

    let ignore = false;
    const loadReturns = async () => {
      try {
        const items = await returnsService.getMyReturnRequests();
        if (ignore) return;
        const orderId = String(order.id || "").trim();
        const filtered = items.filter((item) => String(item.orderId || "").trim() === orderId);
        setReturnRequests(filtered);
      } catch {
        if (!ignore) setReturnRequests([]);
      }
    };

    loadReturns();
    return () => {
      ignore = true;
    };
  }, [order, user]);

  return (
    <section className="ordersPage">
      <div className="container ordersWrap">
        <p className="ordersBreadcrumb">
          <Link to="/orders">Your Orders</Link>
          <span aria-hidden="true">{">"}</span>
          <span>Track Order</span>
        </p>

        <div className="ordersHeader">
          <h1 className="ordersTitle">Track Order</h1>
        </div>

        <div className="profileCard">
          <form className="profileForm" onSubmit={onTrack}>
            <label className="profileField">
              <span>Order ID</span>
              <input
                name="orderId"
                value={form.orderId}
                onChange={handleChange}
                placeholder="Enter your order id"
              />
              {errors.orderId && <small>{errors.orderId}</small>}
            </label>

            <label className="profileField">
              <span>Phone or Email</span>
              <input
                name="contact"
                value={form.contact}
                onChange={handleChange}
                placeholder="Registered phone or email"
              />
              {errors.contact && <small>{errors.contact}</small>}
            </label>

            <div className="profileFormActions">
              <button type="submit" className="profileBtn profileBtnPrimary" disabled={tracking}>
                {tracking ? "Tracking..." : "Track Order"}
              </button>
            </div>
          </form>
        </div>

        {order && (
          <>
            <article className="ordersCard">
              <div className="ordersCardMeta">
                <div>
                  <span>Order #</span>
                  <strong>{order.orderNumber || order.id || "N/A"}</strong>
                </div>
                <div>
                  <span>Tracking #</span>
                  <strong>{order.trackingNumber || "N/A"}</strong>
                </div>
                <div>
                  <span>Status</span>
                  <strong>{order.status || "Pending"}</strong>
                </div>
                <div>
                  <span>Total</span>
                  <strong>{formatCurrency(orderTotal)}</strong>
                </div>
              </div>

              <div className="ordersCardBody">
                <div className="ordersCardContent">
                  <h2>Delivery Status</h2>
                  <p>Placed on {formatOrderDate(order.createdAt)}</p>

                  <div className="mt-3 grid gap-2">
                    {ORDER_STATUSES.map((status, idx) => {
                      const isCompleted = idx <= statusIndex;
                      const isCurrent = idx === statusIndex;

                      return (
                        <div
                          key={status}
                          className={[
                            "rounded-lg border px-3 py-2 text-sm",
                            isCurrent
                              ? "border-emerald-300/60 bg-emerald-500/15 text-emerald-100"
                              : isCompleted
                              ? "border-white/30 bg-white/10 text-[color:var(--text)]"
                              : "border-white/15 bg-white/5 text-[color:var(--muted)]",
                          ].join(" ")}
                        >
                          {status}
                        </div>
                      );
                    })}
                  </div>

                  {returnRequests.length > 0 ? (
                    <div className="ordersReturnTracking">
                      <p className="ordersReturnTrackingTitle">Return Tracking</p>
                      {returnRequests.map((request) => {
                        const returnStatus = getReturnDisplayStatus(request.status);
                        const returnTrackingIndex = getReturnTrackingIndex(request.status);
                        const isRejected = normalizeReturnStatus(request.status) === "rejected";

                        return (
                          <div key={request.id} style={{ marginTop: "10px" }}>
                            <p className="ordersReturnTrackingMeta">
                              Request #{request.id} | Status: {returnStatus || "Pending"}
                            </p>
                            <p className="ordersReturnTrackingMeta">
                              Updated: {formatOrderDate(request.updatedAt || request.createdAt)}
                            </p>

                            {isRejected ? (
                              <p className="ordersReturnTrackingRejected">
                                Request rejected. Please contact support for help with this order.
                              </p>
                            ) : (
                              <div className="ordersReturnTimeline">
                                {RETURN_TRACKING_STEPS.map((step, index) => {
                                  const isDone = returnTrackingIndex >= index;
                                  const isCurrent = returnTrackingIndex === index;
                                  return (
                                    <span
                                      key={`${request.id}_${step}`}
                                      className={`ordersReturnStep ${isDone ? "isDone" : ""} ${
                                        isCurrent ? "isCurrent" : ""
                                      }`}
                                    >
                                      {step}
                                    </span>
                                  );
                                })}
                              </div>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  ) : null}

                  <div className="profileActions mt-4">
                    <button type="button" className="profileBtn profileBtnGhost" onClick={onShare}>
                      Share Tracking
                    </button>
                    <Link to="/orders" className="profileBtn profileBtnGhost">
                      View All Orders
                    </Link>
                  </div>
                </div>
              </div>
            </article>

            <div className="profileCard">
              <h2 className="profileSectionTitle">Items In This Order</h2>
              <p className="profileSubtext">
                {itemCount} item{itemCount === 1 ? "" : "s"} in this order.
              </p>

              {items.length === 0 ? (
                <div className="ordersPlaceholder mt-4">No item details available for this order.</div>
              ) : (
                <div className="mt-4 grid gap-3">
                  {items.map((item, index) => {
                    const qty = Number(item?.qty) || 1;
                    const variantText = [
                      item?.size ? `Size: ${item.size}` : "",
                      item?.color ? `Color: ${item.color}` : "",
                    ]
                      .filter(Boolean)
                      .join(" | ");

                    return (
                      <article
                        key={`${item?.key || item?.productId || index}_${index}`}
                        className="flex items-center justify-between gap-3 rounded-xl border border-white/20 bg-white/5 p-3"
                      >
                        <div className="flex min-w-0 items-center gap-3">
                          {item?.image ? (
                            <img
                              src={item.image}
                              alt={item?.title || `Item ${index + 1}`}
                              className="h-16 w-16 rounded-lg border border-white/15 object-cover"
                            />
                          ) : (
                            <div className="h-16 w-16 rounded-lg border border-white/15 bg-white/10" aria-hidden="true" />
                          )}

                          <div className="min-w-0">
                            <h3 className="truncate text-base font-semibold">{item?.title || `Item ${index + 1}`}</h3>
                            {variantText ? <p className="text-sm text-[color:var(--muted)]">{variantText}</p> : null}
                          </div>
                        </div>

                        <div className="text-right">
                          <p className="font-semibold">{formatCurrency(item?.price)}</p>
                          <p className="text-sm text-[color:var(--muted)]">Qty: {qty}</p>
                        </div>
                      </article>
                    );
                  })}
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </section>
  );
}
