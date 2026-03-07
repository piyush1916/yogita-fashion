import { STORAGE_KEYS } from "../utils/constants";

function safeParse(json, fallback) {
  try {
    const parsed = JSON.parse(json);
    return parsed ?? fallback;
  } catch {
    return fallback;
  }
}

const wishlistService = {
  getItems() {
    const raw = localStorage.getItem(STORAGE_KEYS.WISHLIST);
    const items = raw ? safeParse(raw, []) : [];
    return Array.isArray(items) ? items : [];
  },

  saveItems(items) {
    localStorage.setItem(STORAGE_KEYS.WISHLIST, JSON.stringify(Array.isArray(items) ? items : []));
  },

  clear() {
    localStorage.removeItem(STORAGE_KEYS.WISHLIST);
  },
};

export default wishlistService;
