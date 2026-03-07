import apiClient from "../api/axios";
import { API } from "../api/endpoints";

function toNumber(value, fallback = 0) {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
}

function normalizeReturn(rawReturn) {
  if (!rawReturn || typeof rawReturn !== "object") return null;
  const id = rawReturn.id ?? rawReturn.Id;
  if (id === undefined || id === null) return null;

  return {
    id: String(id),
    orderId: String(rawReturn.orderId ?? rawReturn.OrderId ?? ""),
    userId: String(rawReturn.userId ?? rawReturn.UserId ?? ""),
    itemProductId: String(rawReturn.itemProductId ?? rawReturn.ItemProductId ?? ""),
    itemTitle: String(rawReturn.itemTitle ?? rawReturn.ItemTitle ?? ""),
    quantity: Math.max(1, Math.trunc(toNumber(rawReturn.quantity ?? rawReturn.Quantity, 1))),
    refundAmount: toNumber(rawReturn.refundAmount ?? rawReturn.RefundAmount, 0),
    reason: String(rawReturn.reason ?? rawReturn.Reason ?? "").trim(),
    status: String(rawReturn.status ?? rawReturn.Status ?? "Pending").trim(),
    customerRemark: String(rawReturn.customerRemark ?? rawReturn.CustomerRemark ?? "").trim(),
    adminRemark: String(rawReturn.adminRemark ?? rawReturn.AdminRemark ?? "").trim(),
    createdAt: rawReturn.createdAt ?? rawReturn.CreatedAt ?? "",
    updatedAt: rawReturn.updatedAt ?? rawReturn.UpdatedAt ?? "",
  };
}

export async function getReturnRequests() {
  const response = await apiClient.get(API.RETURNS);
  const items = Array.isArray(response?.data) ? response.data : [];
  return items.map(normalizeReturn).filter(Boolean);
}

export async function updateReturnStatus(id, { status, adminRemark }) {
  const response = await apiClient.patch(`${API.RETURNS}/${id}/status`, {
    status,
    adminRemark,
  });
  return normalizeReturn(response?.data);
}
