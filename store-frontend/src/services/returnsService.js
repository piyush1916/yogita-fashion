import axios from "../api/axios";
import { API } from "../api/endpoints";
import { STORAGE_KEYS } from "../utils/constants";

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
    status: String(rawReturn.status ?? rawReturn.Status ?? "Pending").trim(),
    reason: String(rawReturn.reason ?? rawReturn.Reason ?? "").trim(),
    customerRemark: String(rawReturn.customerRemark ?? rawReturn.CustomerRemark ?? "").trim(),
    adminRemark: String(rawReturn.adminRemark ?? rawReturn.AdminRemark ?? "").trim(),
    refundAmount: Number(rawReturn.refundAmount ?? rawReturn.RefundAmount ?? 0) || 0,
    createdAt: rawReturn.createdAt ?? rawReturn.CreatedAt ?? "",
    updatedAt: rawReturn.updatedAt ?? rawReturn.UpdatedAt ?? "",
  };
}

async function createReturnRequest(payload) {
  const token = String(localStorage.getItem(STORAGE_KEYS.AUTH_TOKEN) || "").trim();
  if (!token) {
    throw new Error("Please login again to request a return.");
  }

  try {
    const response = await axios.post(API.RETURNS, payload);
    return normalizeReturn(response?.data);
  } catch (error) {
    if (error?.response?.status === 401 || error?.response?.status === 403) {
      throw new Error("Please login again to request a return.");
    }
    throw error;
  }
}

async function getMyReturnRequests() {
  const token = String(localStorage.getItem(STORAGE_KEYS.AUTH_TOKEN) || "").trim();
  if (!token) {
    return [];
  }

  try {
    const response = await axios.get(API.RETURNS_MY);
    const items = Array.isArray(response?.data) ? response.data : [];
    return items.map(normalizeReturn).filter(Boolean);
  } catch (error) {
    if (error?.response?.status === 401 || error?.response?.status === 403) {
      return [];
    }
    throw error;
  }
}

export default { createReturnRequest, getMyReturnRequests };
