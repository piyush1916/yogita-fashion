import axios from "../api/axios";
import { API } from "../api/endpoints";

function normalizeText(value) {
  return String(value || "").trim();
}

function normalizeEmail(value) {
  return normalizeText(value).toLowerCase();
}

function normalizePhone(value) {
  return normalizeText(value).replace(/\D+/g, "");
}

function normalizeRequest(rawRequest) {
  if (!rawRequest || typeof rawRequest !== "object") return null;
  const id = rawRequest.id ?? rawRequest.Id;
  if (id === undefined || id === null || id === "") return null;

  return {
    id: String(id),
    userId: Number(rawRequest.userId ?? rawRequest.UserId) || 0,
    name: normalizeText(rawRequest.name ?? rawRequest.Name),
    contact: normalizeText(rawRequest.contact ?? rawRequest.Contact),
    subject: normalizeText(rawRequest.subject ?? rawRequest.Subject) || "General Support",
    message: normalizeText(rawRequest.message ?? rawRequest.Message),
    orderId: normalizeText(rawRequest.orderId ?? rawRequest.OrderId),
    email: normalizeEmail(rawRequest.email ?? rawRequest.Email),
    phone: normalizePhone(rawRequest.phone ?? rawRequest.Phone),
    status: normalizeText(rawRequest.status ?? rawRequest.Status) || "Open",
    createdAt: normalizeText(rawRequest.createdAt ?? rawRequest.CreatedAt) || new Date().toISOString(),
    updatedAt: normalizeText(rawRequest.updatedAt ?? rawRequest.UpdatedAt) || new Date().toISOString(),
  };
}

function toPayload(payload, user) {
  return {
    userId: Number(user?.id) || 0,
    name: normalizeText(payload?.name || user?.name),
    contact: normalizeText(payload?.contact || user?.email || user?.phone),
    subject: normalizeText(payload?.subject) || "General Support",
    message: normalizeText(payload?.message),
    orderId: normalizeText(payload?.orderId),
    email: normalizeEmail(user?.email),
    phone: normalizePhone(user?.phone),
    status: "Open",
  };
}

function parseError(error, fallback) {
  if (!error?.response) return fallback;
  if (typeof error.response.data?.message === "string") return error.response.data.message;
  if (typeof error.response.data === "string") return error.response.data;
  return fallback;
}

const supportService = {
  async createRequest(payload, user) {
    const requestPayload = toPayload(payload, user);
    try {
      const response = await axios.post(API.SUPPORT, requestPayload);
      const request = normalizeRequest(response?.data);
      if (!request) {
        throw new Error("Invalid support API response.");
      }
      return request;
    } catch (error) {
      throw new Error(parseError(error, "Unable to submit support request."));
    }
  },

  async listRequestsByUser() {
    try {
      const response = await axios.get(API.SUPPORT_MY);
      const requests = Array.isArray(response?.data) ? response.data : [];
      return requests
        .map(normalizeRequest)
        .filter(Boolean)
        .sort((a, b) => Date.parse(b?.createdAt || 0) - Date.parse(a?.createdAt || 0));
    } catch (error) {
      if (error?.response?.status === 401 || error?.response?.status === 403) {
        return [];
      }
      throw new Error(parseError(error, "Unable to load support requests."));
    }
  },
};

export default supportService;
