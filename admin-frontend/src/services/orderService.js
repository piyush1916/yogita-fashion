import apiClient from "../api/axios";
import { API } from "../api/endpoints";

function toNumber(value, fallback = 0) {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
}

function normalizeOrder(rawOrder) {
  if (!rawOrder || typeof rawOrder !== "object") return null;

  const id = rawOrder.id ?? rawOrder.Id;
  if (id === undefined || id === null) return null;

  const items = Array.isArray(rawOrder.items ?? rawOrder.Items) ? rawOrder.items ?? rawOrder.Items : [];
  const computedTotal = items.reduce((sum, item) => sum + toNumber(item?.price ?? item?.Price, 0) * toNumber(item?.qty ?? item?.Qty, 1), 0);
  const total = toNumber(rawOrder.total ?? rawOrder.Total, computedTotal);

  const statusHistory = Array.isArray(rawOrder.statusHistory ?? rawOrder.StatusHistory)
    ? rawOrder.statusHistory ?? rawOrder.StatusHistory
    : [];

  return {
    id: String(id),
    orderNumber: String(rawOrder.orderNumber ?? rawOrder.OrderNumber ?? "").trim(),
    userId: toNumber(rawOrder.userId ?? rawOrder.UserId, 0),
    customerName: String(rawOrder.name ?? rawOrder.Name ?? "").trim(),
    email: String(rawOrder.email ?? rawOrder.Email ?? "").trim().toLowerCase(),
    phone: String(rawOrder.phone ?? rawOrder.Phone ?? "").trim(),
    address: String(rawOrder.address ?? rawOrder.Address ?? "").trim(),
    city: String(rawOrder.city ?? rawOrder.City ?? "").trim(),
    pincode: String(rawOrder.pincode ?? rawOrder.Pincode ?? "").trim(),
    payment: String(rawOrder.payment ?? rawOrder.Payment ?? "COD").trim(),
    status: String(rawOrder.status ?? rawOrder.Status ?? "Pending").trim(),
    trackingNumber: String(rawOrder.trackingNumber ?? rawOrder.TrackingNumber ?? "").trim(),
    createdAt: rawOrder.createdAt ?? rawOrder.CreatedAt ?? "",
    updatedAt: rawOrder.updatedAt ?? rawOrder.UpdatedAt ?? "",
    itemCount: items.reduce((sum, item) => sum + toNumber(item?.qty ?? item?.Qty, 1), 0),
    total,
    statusHistory: statusHistory.map((entry) => ({
      status: String(entry?.status ?? entry?.Status ?? "").trim(),
      notes: String(entry?.notes ?? entry?.Notes ?? "").trim(),
      updatedBy: String(entry?.updatedBy ?? entry?.UpdatedBy ?? "").trim(),
      updatedAt: entry?.updatedAt ?? entry?.UpdatedAt ?? "",
    })),
  };
}

export async function getOrders() {
  const response = await apiClient.get(API.ORDERS);
  const orders = Array.isArray(response?.data) ? response.data : [];

  return orders
    .map(normalizeOrder)
    .filter(Boolean)
    .sort((a, b) => {
      const aDate = Date.parse(a.createdAt || 0);
      const bDate = Date.parse(b.createdAt || 0);
      return (Number.isFinite(bDate) ? bDate : 0) - (Number.isFinite(aDate) ? aDate : 0);
    });
}

export async function updateOrderStatus(orderId, { status, notes }) {
  const response = await apiClient.put(`${API.ORDER_STATUS}/${orderId}/status`, {
    status,
    notes,
  });
  return normalizeOrder(response?.data);
}
