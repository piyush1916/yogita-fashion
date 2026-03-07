import { STORAGE_KEYS } from "../utils/constants";

function safeParse(json, fallback) {
  try {
    const parsed = JSON.parse(json);
    return parsed ?? fallback;
  } catch {
    return fallback;
  }
}

function toUserKey(user) {
  const email = String(user?.email || "")
    .trim()
    .toLowerCase();
  const phone = String(user?.phone || "").trim();
  const id = String(user?.id || "").trim();
  return email || phone || id || "guest";
}

function loadAll() {
  const raw = localStorage.getItem(STORAGE_KEYS.ADDRESSES);
  const value = raw ? safeParse(raw, {}) : {};
  return value && typeof value === "object" && !Array.isArray(value) ? value : {};
}

function saveAll(map) {
  localStorage.setItem(STORAGE_KEYS.ADDRESSES, JSON.stringify(map));
}

const addressService = {
  listByUser(user) {
    const map = loadAll();
    const userKey = toUserKey(user);
    const addresses = map[userKey];
    return Array.isArray(addresses) ? addresses : [];
  },

  saveByUser(user, addresses) {
    const map = loadAll();
    const userKey = toUserKey(user);
    map[userKey] = Array.isArray(addresses) ? addresses : [];
    saveAll(map);
  },
};

export default addressService;
