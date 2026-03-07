import apiClient from "../api/axios";
import { API } from "../api/endpoints";

const ADMIN_SESSION_KEY = "yf_admin_session";
const ADMIN_TOKEN_KEY = "yf_admin_token";

function safeParse(json, fallback) {
  try {
    const parsed = JSON.parse(json);
    return parsed ?? fallback;
  } catch {
    return fallback;
  }
}

function normalizeUser(rawUser) {
  if (!rawUser || typeof rawUser !== "object") return null;
  const id = rawUser.id ?? rawUser.Id;
  if (id === undefined || id === null) return null;

  return {
    id: String(id),
    name: String(rawUser.name ?? rawUser.Name ?? "").trim(),
    email: String(rawUser.email ?? rawUser.Email ?? "")
      .trim()
      .toLowerCase(),
    phone: String(rawUser.phone ?? rawUser.Phone ?? "").trim(),
    city: String(rawUser.city ?? rawUser.City ?? "").trim(),
    role: String(rawUser.role ?? rawUser.Role ?? "Admin").trim(),
  };
}

function resolveAuthResponse(rawPayload) {
  const token = String(rawPayload?.token ?? rawPayload?.Token ?? "").trim();
  const userPayload = rawPayload?.user ?? rawPayload?.User ?? rawPayload;
  const user = normalizeUser(userPayload);
  return { token, user };
}

export async function loginAdmin({ email, password }) {
  const payload = {
    email: String(email || "")
      .trim()
      .toLowerCase(),
    password: String(password || ""),
  };

  const response = await apiClient.post(API.LOGIN, payload);
  const { user, token } = resolveAuthResponse(response?.data);
  if (!user) {
    throw new Error("Login response is invalid.");
  }

  if (String(user.role || "").toLowerCase() !== "admin") {
    throw new Error("Only admin users can access this panel.");
  }

  if (token) {
    localStorage.setItem(ADMIN_TOKEN_KEY, token);
  } else {
    localStorage.removeItem(ADMIN_TOKEN_KEY);
  }
  localStorage.setItem(ADMIN_SESSION_KEY, JSON.stringify(user));
  return user;
}

export function getAdminSession() {
  const token = getAdminToken();
  if (!token) return null;

  const raw = localStorage.getItem(ADMIN_SESSION_KEY);
  if (!raw) return null;
  const user = normalizeUser(safeParse(raw, null));
  if (!user) return null;
  if (String(user.role || "").toLowerCase() !== "admin") return null;
  return user;
}

export function logoutAdmin() {
  localStorage.removeItem(ADMIN_SESSION_KEY);
  localStorage.removeItem(ADMIN_TOKEN_KEY);
}

export function getAdminToken() {
  return localStorage.getItem(ADMIN_TOKEN_KEY) || "";
}
