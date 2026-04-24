import apiClient from "../api/axios";
import { API } from "../api/endpoints";

function normalizeText(value) {
  return String(value ?? "").trim();
}

function normalizeSupportRequest(rawItem) {
  if (!rawItem || typeof rawItem !== "object") return null;

  const id = rawItem.id ?? rawItem.Id;
  if (id === undefined || id === null) return null;

  return {
    id: String(id),
    userId: Number(rawItem.userId ?? rawItem.UserId) || 0,
    name: normalizeText(rawItem.name ?? rawItem.Name),
    contact: normalizeText(rawItem.contact ?? rawItem.Contact),
    email: normalizeText(rawItem.email ?? rawItem.Email).toLowerCase(),
    phone: normalizeText(rawItem.phone ?? rawItem.Phone),
    subject: normalizeText(rawItem.subject ?? rawItem.Subject) || "General Support",
    message: normalizeText(rawItem.message ?? rawItem.Message),
    orderId: normalizeText(rawItem.orderId ?? rawItem.OrderId),
    status: normalizeText(rawItem.status ?? rawItem.Status) || "Open",
    createdAt: rawItem.createdAt ?? rawItem.CreatedAt ?? "",
    updatedAt: rawItem.updatedAt ?? rawItem.UpdatedAt ?? "",
  };
}

export async function getSupportRequests() {
  const response = await apiClient.get(API.SUPPORT_REQUESTS);
  const items = Array.isArray(response?.data) ? response.data : [];
  return items.map(normalizeSupportRequest).filter(Boolean);
}

export async function updateSupportRequestStatus(id, status) {
  const response = await apiClient.patch(`${API.SUPPORT_REQUESTS}/${id}/status`, {
    status,
  });

  return normalizeSupportRequest(response?.data);
}
