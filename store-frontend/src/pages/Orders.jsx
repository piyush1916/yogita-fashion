import React, { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../hooks/useToast";
import ordersService from "../services/ordersService";
import returnsService from "../services/returnsService";
import { formatCurrency } from "../utils/currency";

const ORDER_TABS = [
  { id: "orders", label: "Orders" },
  { id: "buy-again", label: "Buy Again" },
  { id: "not-shipped", label: "Not Yet Shipped" },
];

const TIME_FILTERS = [
  { value: "90", label: "past 3 months" },
  { value: "180", label: "past 6 months" },
  { value: "365", label: "past year" },
  { value: "all", label: "all time" },
];

const NOT_SHIPPED_STATUSES = new Set(["Pending", "Confirmed", "Packed"]);

const parseDate = (iso) => {
  const ts = Date.parse(iso || "");
  return Number.isFinite(ts) ? ts : null;
};

const formatOrderDate = (iso) => {
  const ts = parseDate(iso);
  if (!ts) return "N/A";
  return new Date(ts).toLocaleDateString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
};

const formatDateTime = (iso) => {
  const ts = parseDate(iso);
  if (!ts) return "N/A";
  return new Date(ts).toLocaleString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
};

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

const orderMatchesSearch = (order, query) => {
  if (!query) return true;
  const q = query.toLowerCase();
  const fields = [
    order.id,
    order.orderNumber,
    order.trackingNumber,
    order.status,
    ...(Array.isArray(order.items) ? order.items.map((item) => item?.title) : []),
  ]
    .filter(Boolean)
    .map((value) => String(value).toLowerCase());

  return fields.some((value) => value.includes(q));
};

export default function Orders() {
  const { user } = useAuth();
  const toast = useToast();
  const [allOrders, setAllOrders] = useState([]);
  const [myReturns, setMyReturns] = useState([]);
  const [loading, setLoading] = useState(true);
  const [requestingReturnKey, setRequestingReturnKey] = useState("");
  const [query, setQuery] = useState("");
  const [activeTab, setActiveTab] = useState("orders");
  const [timeFilter, setTimeFilter] = useState("90");

  useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        setLoading(true);
        const [ordersResult, returnsResult] = await Promise.allSettled([
          ordersService.listOrders(user),
          user ? returnsService.getMyReturnRequests() : Promise.resolve([]),
        ]);
        const orders = ordersResult.status === "fulfilled" ? ordersResult.value : [];
        const returns = returnsResult.status === "fulfilled" ? returnsResult.value : [];
        if (mounted) {
          setAllOrders(Array.isArray(orders) ? orders : []);
          setMyReturns(Array.isArray(returns) ? returns : []);
        }
      } finally {
        if (mounted) setLoading(false);
      }
    };

    load();
    return () => {
      mounted = false;
    };
  }, [user]);

  const returnRequestByKey = useMemo(() => {
    return myReturns.reduce((acc, item) => {
      const key = `${item.orderId}_${item.itemProductId}`;
      acc[key] = item;
      return acc;
    }, {});
  }, [myReturns]);

  const handleRequestReturn = async (order, leadItem) => {
    const productId = String(leadItem?.productId || "").trim();
    if (!productId) {
      toast.error("Product info missing for return request.");
      return;
    }

    const reason = window.prompt("Return reason (required):", "Size issue");
    if (!reason || !String(reason).trim()) {
      toast.error("Return reason is required.");
      return;
    }

    const returnKey = `${order.id}_${productId}`;
    setRequestingReturnKey(returnKey);
    try {
      const created = await returnsService.createReturnRequest({
        orderId: Number(order.id),
        itemProductId: productId,
        reason: String(reason).trim(),
        customerRemark: "",
      });
      if (created) {
        setMyReturns((prev) => [created, ...prev]);
      }
      toast.success("Return request submitted.");
    } catch (error) {
      const message =
        typeof error?.response?.data?.message === "string"
          ? error.response.data.message
          : typeof error?.message === "string" && error.message.trim()
          ? error.message.trim()
          : "Unable to submit return request.";
      toast.error(message);
    } finally {
      setRequestingReturnKey("");
    }
  };

  const userOrders = useMemo(() => {
    if (!user) return [];
    const userId = Number(user.id) || 0;
    const email = (user.email || "").trim().toLowerCase();
    const phone = String(user.phone || "").replace(/\D+/g, "");

    return allOrders.filter((order) => {
      const orderUserId = Number(order?.userId) || 0;
      if (userId > 0 && orderUserId > 0 && orderUserId === userId) {
        return true;
      }

      const orderEmail = String(order?.email || "")
        .trim()
        .toLowerCase();
      const orderPhone = String(order?.phone || "").replace(/\D+/g, "");
      return (email && orderEmail === email) || (phone && orderPhone === phone);
    });
  }, [allOrders, user]);

  const visibleOrders = useMemo(() => {
    let next = [...userOrders];

    if (timeFilter !== "all") {
      const days = Number(timeFilter);
      if (Number.isFinite(days)) {
        const cutoff = Date.now() - days * 24 * 60 * 60 * 1000;
        next = next.filter((order) => {
          const ts = parseDate(order?.createdAt);
          return ts ? ts >= cutoff : false;
        });
      }
    }

    if (activeTab === "not-shipped") {
      next = next.filter((order) => NOT_SHIPPED_STATUSES.has(order.status));
    }

    if (activeTab === "buy-again") {
      next = next.filter((order) => Array.isArray(order.items) && order.items.length > 0);
    }

    const trimmed = query.trim();
    if (trimmed) {
      next = next.filter((order) => orderMatchesSearch(order, trimmed));
    }

    return next;
  }, [activeTab, query, timeFilter, userOrders]);

  const selectedFilterLabel =
    TIME_FILTERS.find((option) => option.value === timeFilter)?.label || "selected period";

  if (!user) {
    return (
      <section className="ordersPage">
        <div className="container ordersWrap">
          <div className="profileCard profileEmpty">
            <h1 className="ordersTitle">Your Orders</h1>
            <p className="profileSubtext">Please login to see your orders and delivery updates.</p>
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
          <span>Your Orders</span>
        </p>

        <div className="ordersHeader">
          <h1 className="ordersTitle">Your Orders</h1>
          <form className="ordersSearch" onSubmit={(e) => e.preventDefault()}>
            <label htmlFor="orders-search" className="ordersSearchField">
              <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
                <path
                  d="M10.5 3a7.5 7.5 0 105.02 13.08l3.2 3.2a1 1 0 001.41-1.42l-3.2-3.2A7.5 7.5 0 0010.5 3zm0 2a5.5 5.5 0 110 11 5.5 5.5 0 010-11z"
                  fill="currentColor"
                />
              </svg>
              <input
                id="orders-search"
                type="search"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Search all orders"
              />
            </label>
            <button type="submit" className="ordersSearchBtn">
              Search Orders
            </button>
          </form>
        </div>

        <div className="ordersTabs" role="tablist" aria-label="Order sections">
          {ORDER_TABS.map((tab) => (
            <button
              key={tab.id}
              type="button"
              role="tab"
              aria-selected={activeTab === tab.id}
              className={`ordersTab ${activeTab === tab.id ? "isActive" : ""}`}
              onClick={() => setActiveTab(tab.id)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        <div className="ordersFilterRow">
          <p className="ordersCount">
            <strong>{visibleOrders.length} orders</strong> placed in
          </p>
          <select
            className="ordersRange"
            value={timeFilter}
            onChange={(e) => setTimeFilter(e.target.value)}
            aria-label="Select order time range"
          >
            {TIME_FILTERS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        {loading ? (
          <div className="ordersPlaceholder">Loading your orders...</div>
        ) : visibleOrders.length === 0 ? (
          <div className="ordersEmptyState">
            <p>
              Looks like you haven&apos;t placed an order in the {selectedFilterLabel}.{" "}
              <button type="button" onClick={() => setTimeFilter("all")}>
                View all orders
              </button>
            </p>

            <div className="ordersSuggestion">
              <div className="ordersSuggestionGraphic" aria-hidden="true" />
              <div>
                <h2>Fresh arrivals waiting for you</h2>
                <p>Shop trending collections and place your next order in just a few clicks.</p>
                <Link to="/shop" className="profileBtn profileBtnPrimary">
                  Continue Shopping
                </Link>
              </div>
            </div>
          </div>
        ) : (
          <div className="ordersList">
            {visibleOrders.map((order) => {
              const leadItem = order.items?.[0];
              const quantity = Array.isArray(order.items)
                ? order.items.reduce((sum, item) => sum + (Number(item?.qty) || 0), 0)
                : 0;
              const canShowMrp = Number(leadItem?.mrp) > Number(leadItem?.price);
              const leadProductId = String(leadItem?.productId || "").trim();
              const returnKey = `${order.id}_${leadProductId}`;
              const returnRequest = returnRequestByKey[returnKey] || null;
              const returnStatus = returnRequest?.status || "";
              const returnDisplayStatus = getReturnDisplayStatus(returnStatus);
              const returnTrackingIndex = getReturnTrackingIndex(returnStatus);
              const isReturnRejected = normalizeReturnStatus(returnStatus) === "rejected";
              const canRequestReturn =
                String(order.status || "").toLowerCase() === "delivered" && Boolean(leadProductId) && !returnRequest;

              return (
                <article key={order.id} className="ordersCard">
                  <div className="ordersCardMeta">
                    <div>
                      <span>Order placed</span>
                      <strong>{formatOrderDate(order.createdAt)}</strong>
                    </div>
                    <div>
                      <span>Total</span>
                      <strong>{formatCurrency(order.total)}</strong>
                    </div>
                    <div>
                      <span>Order #</span>
                      <strong>{order.orderNumber || order.id}</strong>
                    </div>
                    <div>
                      <span>Status</span>
                      <strong>{order.status || "Pending"}</strong>
                    </div>
                  </div>

                  <div className="ordersCardBody">
                    <div className="ordersCardImageWrap">
                      {leadItem?.image ? (
                        <img src={leadItem.image} alt={leadItem.title || "Ordered item"} className="ordersCardImage" />
                      ) : (
                        <div className="ordersCardFallback" aria-hidden="true" />
                      )}
                    </div>

                    <div className="ordersCardContent">
                      <h2>{leadItem?.title || "Order items"}</h2>
                      <p>
                        {quantity} item{quantity === 1 ? "" : "s"} | Tracking ID {order.trackingNumber || "N/A"}
                      </p>
                      <div className="ordersPriceRow">
                        <strong>{formatCurrency(leadItem?.price || order.total)}</strong>
                        {canShowMrp && <span>{formatCurrency(leadItem.mrp)}</span>}
                      </div>
                      <div className="ordersCardActions">
                        <Link
                          to="/track-order"
                          state={{ orderId: order.orderNumber || order.id, contact: order.email || order.phone || "" }}
                          className="profileBtn profileBtnGhost"
                        >
                          Track Order
                        </Link>
                        {canRequestReturn ? (
                          <button
                            type="button"
                            className="profileBtn profileBtnGhost"
                            disabled={requestingReturnKey === returnKey}
                            onClick={() => handleRequestReturn(order, leadItem)}
                          >
                            {requestingReturnKey === returnKey ? "Submitting..." : "Request Return"}
                          </button>
                        ) : null}
                        {returnDisplayStatus ? <span className="ordersReturnChip">Return: {returnDisplayStatus}</span> : null}
                      </div>

                      {returnRequest ? (
                        <div className="ordersReturnTracking">
                          <p className="ordersReturnTrackingTitle">Return Tracking</p>
                          <p className="ordersReturnTrackingMeta">
                            Updated: {formatDateTime(returnRequest.updatedAt || returnRequest.createdAt)}
                          </p>

                          {isReturnRejected ? (
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
                                    key={`${returnRequest.id}-${step}`}
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

                          {returnRequest.adminRemark ? (
                            <p className="ordersReturnTrackingMeta">Admin note: {returnRequest.adminRemark}</p>
                          ) : null}

                          {Number(returnRequest.refundAmount) > 0 ? (
                            <p className="ordersReturnTrackingMeta">
                              Refund amount: {formatCurrency(returnRequest.refundAmount)}
                            </p>
                          ) : null}
                        </div>
                      ) : null}
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
        )}
      </div>
    </section>
  );
}
