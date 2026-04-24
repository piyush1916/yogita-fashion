import axios from "../api/axios";
import { API } from "../api/endpoints";
import { STORAGE_KEYS } from "../utils/constants";

const SERVER_UNAVAILABLE_MESSAGE = "Server unavailable. Please ensure backend API is running and try again.";

function safeParse(json, fallback) {
  try {
    const parsed = JSON.parse(json);
    return parsed ?? fallback;
  } catch {
    return fallback;
  }
}

function normalizeUser(user) {
  if (!user || typeof user !== "object") return null;
  const idValue = user.id ?? user.Id;
  if (typeof idValue === "undefined" || idValue === null || idValue === "") return null;

  return {
    id: String(idValue),
    name: String(user.name ?? user.Name ?? "").trim(),
    email: String(user.email ?? user.Email ?? "").trim().toLowerCase(),
    password: String(user.password ?? user.Password ?? ""),
    phone: String(user.phone ?? user.Phone ?? "").trim(),
    city: String(user.city ?? user.City ?? "").trim(),
    role: String(user.role ?? user.Role ?? "Customer").trim(),
    createdAt: String(user.createdAt ?? user.CreatedAt ?? "").trim(),
  };
}

function parseAuthResponse(rawResponse) {
  if (!rawResponse || typeof rawResponse !== "object") {
    return { user: null, token: "" };
  }

  const token = String(rawResponse.token ?? rawResponse.Token ?? "").trim();
  const userPayload = rawResponse.user ?? rawResponse.User ?? rawResponse;
  const user = normalizeUser(userPayload);
  return { user, token };
}

const getUsers = () => {
  const raw = localStorage.getItem(STORAGE_KEYS.AUTH_USERS);
  const users = raw ? safeParse(raw, []) : [];
  return Array.isArray(users) ? users.map(normalizeUser).filter(Boolean) : [];
};

const saveUsers = (users) => {
  localStorage.setItem(STORAGE_KEYS.AUTH_USERS, JSON.stringify(Array.isArray(users) ? users : []));
};

const getSession = () => {
  const raw = localStorage.getItem(STORAGE_KEYS.AUTH_SESSION);
  const session = raw ? safeParse(raw, null) : null;
  return normalizeUser(session);
};

const saveSession = (session) => {
  localStorage.setItem(STORAGE_KEYS.AUTH_SESSION, JSON.stringify(session || null));
};

const saveToken = (token) => {
  const safeToken = String(token || "").trim();
  if (!safeToken) {
    localStorage.removeItem(STORAGE_KEYS.AUTH_TOKEN);
    return;
  }
  localStorage.setItem(STORAGE_KEYS.AUTH_TOKEN, safeToken);
};

const getToken = () => String(localStorage.getItem(STORAGE_KEYS.AUTH_TOKEN) || "").trim();

const syncUser = (user, token = "") => {
  if (!user) return null;
  const users = getUsers();
  const idx = users.findIndex((u) => u.id === user.id || u.email === user.email);
  if (idx >= 0) users[idx] = { ...users[idx], ...user };
  else users.push(user);
  saveUsers(users);
  saveSession(user);
  saveToken(token);
  return user;
};

const registerLocal = async ({ name, email, password, phone, city }) => {
  const users = getUsers();
  const normalizedEmail = String(email || "").trim().toLowerCase();
  if (users.find((u) => u.email === normalizedEmail)) throw new Error("Email already registered");

  const user = {
    id: String(Date.now()),
    name: String(name || "").trim(),
    email: normalizedEmail,
    password: String(password || ""),
    phone: String(phone || "").trim(),
    city: String(city || "").trim(),
    role: "Customer",
    createdAt: new Date().toISOString(),
  };
  users.push(user);
  saveUsers(users);
  saveSession(user);
  return user;
};

const loginLocal = async ({ email, password }) => {
  const normalizedEmail = String(email || "").trim().toLowerCase();
  const users = getUsers();
  const user = users.find((u) => u.email === normalizedEmail && u.password === String(password || ""));
  if (!user) throw new Error("Invalid credentials");
  saveSession(user);
  return user;
};

const updateProfileLocal = async (payload) => {
  const session = getSession();
  if (!session) throw new Error("Please login first");

  const users = getUsers();
  const index = users.findIndex((u) => u.id === session.id);
  const nextEmail = String(payload?.email ?? session.email)
    .trim()
    .toLowerCase();

  if (nextEmail) {
    const duplicate = users.find((u, idx) => idx !== index && u.email === nextEmail);
    if (duplicate) throw new Error("Email already registered");
  }

  const updated = {
    ...session,
    ...payload,
    email: nextEmail,
    phone: String(payload?.phone ?? session.phone ?? "").trim(),
    city: String(payload?.city ?? session.city ?? "").trim(),
    name: String(payload?.name ?? session.name ?? "").trim(),
    createdAt: String(payload?.createdAt ?? session.createdAt ?? "").trim(),
  };

  if (index >= 0) {
    users[index] = updated;
    saveUsers(users);
  }
  saveSession(updated);
  return updated;
};

const register = async ({ name, email, password, phone = "", city = "" }) => {
  const payload = {
    name: String(name || "").trim(),
    email: String(email || "").trim().toLowerCase(),
    password: String(password || ""),
    phone: String(phone || "").trim(),
    city: String(city || "").trim(),
  };

  try {
    const res = await axios.post(API.REGISTER, payload);
    const { user, token } = parseAuthResponse(res?.data);
    if (!user) throw new Error("Invalid register response");
    return syncUser(user, token);
  } catch (error) {
    if (error?.response) {
      const message = typeof error.response.data === "string" ? error.response.data : "Unable to register.";
      throw new Error(message);
    }
    throw new Error(SERVER_UNAVAILABLE_MESSAGE);
  }
};

const login = async ({ email, password }) => {
  const payload = {
    email: String(email || "").trim().toLowerCase(),
    password: String(password || ""),
  };

  try {
    const res = await axios.post(API.LOGIN, payload);
    const { user, token } = parseAuthResponse(res?.data);
    if (!user) throw new Error("Invalid login response");
    return syncUser(user, token);
  } catch (error) {
    if (error?.response) {
      const message = typeof error.response.data === "string" ? error.response.data : "Invalid email or password.";
      throw new Error(message);
    }
    throw new Error(SERVER_UNAVAILABLE_MESSAGE);
  }
};

const updateProfile = async (payload) => {
  const session = getSession();
  if (!session) throw new Error("Please login first");
  if (!getToken()) throw new Error("Please login again.");

  const apiPayload = {
    name: String(payload?.name ?? session.name ?? "").trim(),
    email: String(payload?.email ?? session.email ?? "").trim().toLowerCase(),
    phone: String(payload?.phone ?? session.phone ?? "").trim(),
    city: String(payload?.city ?? session.city ?? "").trim(),
  };

  try {
    const res = await axios.put(`${API.AUTH_PROFILE}/${session.id}`, apiPayload);
    const user = normalizeUser(res?.data?.user ?? res?.data);
    if (!user) throw new Error("Invalid profile response");
    return syncUser(user);
  } catch (error) {
    if (error?.response) {
      const message = typeof error.response.data === "string" ? error.response.data : "Unable to update profile.";
      throw new Error(message);
    }
    throw new Error(SERVER_UNAVAILABLE_MESSAGE);
  }
};

const getProfile = async (id) => {
  const targetId = String(id || "").trim();
  if (!targetId) return null;
  if (!getToken()) throw new Error("Please login again.");

  try {
    const res = await axios.get(`${API.AUTH_PROFILE}/${targetId}`);
    const user = normalizeUser(res?.data?.user ?? res?.data);
    if (!user) throw new Error("Invalid profile response");
    return syncUser(user, getToken());
  } catch (error) {
    if (error?.response?.status === 401 || error?.response?.status === 403) {
      logout();
      return null;
    }
    if (error?.response) {
      const message = typeof error.response.data === "string" ? error.response.data : "Unable to load profile.";
      throw new Error(message);
    }
    throw new Error(SERVER_UNAVAILABLE_MESSAGE);
  }
};

const logout = () => {
  localStorage.removeItem(STORAGE_KEYS.AUTH_SESSION);
  localStorage.removeItem(STORAGE_KEYS.AUTH_TOKEN);
};

const profile = () => {
  const session = getSession();
  if (!session) return null;

  if (!getToken()) {
    // Clear stale pseudo-login state from old sessions that do not have JWT token.
    localStorage.removeItem(STORAGE_KEYS.AUTH_SESSION);
    return null;
  }

  return session;
};

export default { register, login, updateProfile, getProfile, logout, profile };
