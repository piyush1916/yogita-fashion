import axios from "../api/axios";
import { API } from "../api/endpoints";
import { STORAGE_KEYS } from "../utils/constants";

const SERVER_UNAVAILABLE_MESSAGE = "Server unavailable. Support request was not submitted. Please try again.";

function safeParse(json, fallback) {
  try {
    const parsed = JSON.parse(json);
    return parsed ?? fallback;
  } catch {
    return fallback;
  }
}

function getAllRequests() {
  const raw = localStorage.getItem(STORAGE_KEYS.SUPPORT_REQUESTS);
  const items = raw ? safeParse(raw, []) : [];
  return Array.isArray(items) ? items : [];
}

function saveAllRequests(items) {
  localStorage.setItem(STORAGE_KEYS.SUPPORT_REQUESTS, JSON.stringify(Array.isArray(items) ? items : []));
}

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
    name: normalizeText(payload?.name),
    contact: normalizeText(payload?.contact),
    subject: normalizeText(payload?.subject) || "General Support",
    message: normalizeText(payload?.message),
    orderId: normalizeText(payload?.orderId),
    email: normalizeEmail(user?.email),
    phone: normalizePhone(user?.phone),
    status: "Open",
  };
}

const supportService = {
  async createRequest(payload, user) {
    const requestPayload = toPayload(payload, user);
    const all = getAllRequests();

    try {
      const response = await axios.post(API.SUPPORT, requestPayload);
      const apiRequest = normalizeRequest(response?.data);
      if (apiRequest) {
        saveAllRequests([apiRequest, ...all]);
        return apiRequest;
      }
      throw new Error("Invalid support API response.");
    } catch (error) {
      if (error?.response) {
        const message = typeof error.response.data === "string" ? error.response.data : "Unable to submit support request.";
        throw new Error(message);
      }
      throw new Error(SERVER_UNAVAILABLE_MESSAGE);
    }
  },

  async listRequestsByUser(user) {
    const email = normalizeEmail(user?.email);
    const phone = normalizePhone(user?.phone);

    if (!email && !phone) return [];

    try {
      const response = await axios.get(API.SUPPORT);
      const requests = Array.isArray(response?.data) ? response.data : [];
      const normalized = requests.map(normalizeRequest).filter(Boolean);
      return normalized
        .filter((item) => {
          const itemEmail = normalizeEmail(item?.email || item?.contact);
          const itemPhone = normalizePhone(item?.phone || item?.contact);
          return (email && itemEmail === email) || (phone && itemPhone === phone);
        })
        .sort((a, b) => Date.parse(b?.createdAt || 0) - Date.parse(a?.createdAt || 0));
    } catch {
      return getAllRequests()
        .map(normalizeRequest)
        .filter(Boolean)
        .filter((item) => {
          const itemEmail = normalizeEmail(item?.email || item?.contact);
          const itemPhone = normalizePhone(item?.phone || item?.contact);
          return (email && itemEmail === email) || (phone && itemPhone === phone);
        })
        .sort((a, b) => Date.parse(b?.createdAt || 0) - Date.parse(a?.createdAt || 0));
    }
  },
};

export default supportService;
