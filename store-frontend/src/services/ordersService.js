import axios from "../api/axios";
import { API } from "../api/endpoints";
import { STORAGE_KEYS, ORDER_STATUSES } from "../utils/constants";

const SERVER_UNAVAILABLE_MESSAGE = "Server unavailable. Order could not be placed. Please try again.";

const delay = (ms) => new Promise((res) => setTimeout(res, ms));
const normalizeText = (value) => String(value ?? "").trim();
const normalizeEmail = (value) => normalizeText(value).toLowerCase();
const normalizePhone = (value) => normalizeText(value).replace(/\D+/g, "");

const safeParse = (json, fallback) => {
  try {
    const parsed = JSON.parse(json);
    return parsed ?? fallback;
  } catch {
    return fallback;
  }
};

const getLocalOrders = () => {
  const json = localStorage.getItem(STORAGE_KEYS.ORDERS);
  const orders = json ? safeParse(json, []) : [];
  return Array.isArray(orders) ? orders : [];
};

const saveLocalOrders = (orders) => {
  localStorage.setItem(STORAGE_KEYS.ORDERS, JSON.stringify(Array.isArray(orders) ? orders : []));
};

const normalizeOrderItem = (item, index = 0) => {
  const productId = normalizeText(item?.productId ?? item?.id);
  return {
    key: normalizeText(item?.key) || `${productId || "item"}_${index}`,
    productId: productId || `item_${index}`,
    title: normalizeText(item?.title) || "Product",
    image: normalizeText(item?.image),
    price: Number(item?.price) || 0,
    mrp: Number(item?.mrp) || Number(item?.price) || 0,
    category: normalizeText(item?.category),
    size: normalizeText(item?.size),
    color: normalizeText(item?.color),
    qty: Math.max(1, Number(item?.qty) || 1),
  };
};

const normalizeApiProductId = (value) => {
  const raw = normalizeText(value);
  if (!raw) return "";

  // Handle legacy cart formats like "12__M__Black" and keep server id numeric.
  const head = raw.split("__")[0].trim();
  if (/^\d+$/.test(head)) return head;

  const fromHead = head.match(/\d+/)?.[0];
  if (fromHead) return fromHead;

  const fromRaw = raw.match(/\d+/)?.[0];
  return fromRaw || head;
};

const normalizeOrder = (order, fallback = {}) => {
  const idValue = order?.id ?? order?.Id ?? fallback?.id ?? Date.now();
  const itemsSource = Array.isArray(order?.items) ? order.items : Array.isArray(fallback?.items) ? fallback.items : [];
  const items = itemsSource.map(normalizeOrderItem);
  const totalFromOrder = Number(order?.total ?? order?.Total ?? order?.totalPrice ?? order?.TotalPrice);
  const computedTotal = items.reduce((sum, item) => sum + item.price * item.qty, 0);
  const total = Number.isFinite(totalFromOrder) && totalFromOrder > 0 ? totalFromOrder : computedTotal;

  return {
    id: String(idValue),
    orderNumber: normalizeText(order?.orderNumber ?? order?.OrderNumber ?? fallback?.orderNumber),
    userId: Number(order?.userId ?? order?.UserId ?? fallback?.userId) || 0,
    name: normalizeText(order?.name ?? order?.Name ?? fallback?.name),
    phone: normalizeText(order?.phone ?? order?.Phone ?? fallback?.phone),
    email: normalizeText(order?.email ?? order?.Email ?? fallback?.email),
    address: normalizeText(order?.address ?? order?.Address ?? fallback?.address),
    city: normalizeText(order?.city ?? order?.City ?? fallback?.city),
    pincode: normalizeText(order?.pincode ?? order?.Pincode ?? fallback?.pincode),
    payment: normalizeText(order?.payment ?? order?.Payment ?? fallback?.payment) || "COD",
    items,
    total,
    status: normalizeText(order?.status ?? order?.Status ?? fallback?.status) || ORDER_STATUSES[0],
    trackingNumber:
      normalizeText(order?.trackingNumber ?? order?.TrackingNumber ?? fallback?.trackingNumber) ||
      `TRK${String(idValue).slice(-6)}`,
    createdAt: normalizeText(order?.createdAt ?? order?.CreatedAt ?? fallback?.createdAt) || new Date().toISOString(),
  };
};

const toApiOrder = (order) => {
  const normalized = normalizeOrder(order);
  return {
    userId: normalized.userId,
    name: normalized.name,
    phone: normalized.phone,
    email: normalized.email,
    address: normalized.address,
    city: normalized.city,
    pincode: normalized.pincode,
    payment: normalized.payment,
    total: normalized.total,
    status: normalized.status,
    items: normalized.items.map((item) => ({
      key: item.key,
      productId: normalizeApiProductId(item.productId),
      title: item.title,
      image: item.image,
      price: item.price,
      mrp: item.mrp,
      category: item.category,
      size: item.size,
      color: item.color,
      qty: item.qty,
    })),
  };
};

const formatIssueSummary = (issues) => {
  if (!Array.isArray(issues) || issues.length === 0) return "";
  const top = issues.slice(0, 3);
  return top
    .map((issue) => {
      const name = normalizeText(issue?.productName ?? issue?.title ?? issue?.productId ?? "Item");
      const available = Number(issue?.available);
      if (Number.isFinite(available)) {
        return `${name} (available: ${Math.max(0, available)})`;
      }
      const issueMessage = normalizeText(issue?.message);
      return issueMessage ? `${name}: ${issueMessage}` : name;
    })
    .join(", ");
};

const createOrderLocal = async (order) => {
  await delay(200);
  const orders = getLocalOrders();
  const id = String(Date.now());
  const datePart = new Date().toISOString().slice(0, 10).replace(/-/g, "");
  const next = normalizeOrder(
    {
      ...order,
      id,
      orderNumber: `YF-${datePart}-${id.slice(-4)}`,
      status: ORDER_STATUSES[0],
      trackingNumber: `TRK${id.slice(-6)}`,
      createdAt: new Date().toISOString(),
    },
    order
  );
  orders.push(next);
  saveLocalOrders(orders);
  return next;
};

const trackOrderLocal = async ({ orderId, contact }) => {
  await delay(100);
  const orders = getLocalOrders().map((order) => normalizeOrder(order));

  const targetOrderId = normalizeText(orderId);
  const targetContactRaw = normalizeText(contact);
  const targetIsEmail = targetContactRaw.includes("@");
  const targetContact = targetIsEmail ? normalizeEmail(targetContactRaw) : normalizePhone(targetContactRaw);

  const found = orders.find((order) => {
    const matchesOrderId =
      normalizeText(order?.id) === targetOrderId || normalizeText(order?.orderNumber).toUpperCase() === targetOrderId.toUpperCase();
    if (!matchesOrderId) return false;
    const orderContact = targetIsEmail ? normalizeEmail(order?.email) : normalizePhone(order?.phone);
    return Boolean(orderContact) && orderContact === targetContact;
  });

  return found || null;
};

const listOrdersLocal = async () => {
  await delay(100);
  const orders = getLocalOrders().map((order) => normalizeOrder(order));
  return [...orders].sort((a, b) => {
    const aTime = Date.parse(a.createdAt || 0);
    const bTime = Date.parse(b.createdAt || 0);
    return (Number.isFinite(bTime) ? bTime : 0) - (Number.isFinite(aTime) ? aTime : 0);
  });
};

const createOrder = async (order) => {
  const normalized = normalizeOrder(order);
  try {
    const res = await axios.post(API.ORDERS, toApiOrder(normalized));
    return normalizeOrder(res?.data, normalized);
  } catch (error) {
    if (error?.response) {
      const issues = Array.isArray(error.response.data?.issues) ? error.response.data.issues : [];
      const issueSummary = formatIssueSummary(issues);
      const message =
        typeof error.response.data?.message === "string"
          ? error.response.data.message
          : typeof error.response.data === "string"
          ? error.response.data
          : "Failed to place order.";

      const nextError = new Error(issueSummary ? `${message} ${issueSummary}` : message);
      if (issues.length > 0) {
        nextError.issues = issues;
      }
      throw nextError;
    }
    throw new Error(SERVER_UNAVAILABLE_MESSAGE);
  }
};

const trackOrder = async ({ orderId, contact }) => {
  const payload = {
    orderId: normalizeText(orderId),
    contact: normalizeText(contact),
  };

  try {
    const res = await axios.post(`${API.ORDERS}/track`, payload);
    return normalizeOrder(res?.data);
  } catch (error) {
    if (error?.response?.status === 404) return null;
    return trackOrderLocal(payload);
  }
};

const listOrders = async () => {
  try {
    const res = await axios.get(`${API.ORDERS}/me`);
    const items = Array.isArray(res?.data) ? res.data : [];
    return items.map((order) => normalizeOrder(order));
  } catch {
    return listOrdersLocal();
  }
};

export default { createOrder, trackOrder, listOrders };
