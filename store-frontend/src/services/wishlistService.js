import axios from "../api/axios";
import { API } from "../api/endpoints";
import { STORAGE_KEYS } from "../utils/constants";

function hasToken() {
  return Boolean(String(localStorage.getItem(STORAGE_KEYS.AUTH_TOKEN) || "").trim());
}

function normalizeProductId(productId) {
  return String(productId ?? "").trim();
}

function safeParse(json, fallback) {
  try {
    const parsed = JSON.parse(json);
    return parsed ?? fallback;
  } catch {
    return fallback;
  }
}

function getLocalItems() {
  const raw = localStorage.getItem(STORAGE_KEYS.WISHLIST);
  const items = raw ? safeParse(raw, []) : [];
  if (!Array.isArray(items)) return [];
  return items
    .map((item) => ({
      productId: normalizeProductId(item?.productId ?? item?.id),
      title: String(item?.title || "").trim(),
      image: String(item?.image || "").trim(),
      price: Number(item?.price) || 0,
      mrp: Number(item?.mrp) || 0,
      category: String(item?.category || "").trim(),
      addedAt: String(item?.addedAt || "").trim() || new Date().toISOString(),
    }))
    .filter((item) => Boolean(item.productId));
}

function saveLocalItems(items) {
  localStorage.setItem(STORAGE_KEYS.WISHLIST, JSON.stringify(Array.isArray(items) ? items : []));
}

function normalizeApiWishlistItem(rawItem) {
  if (!rawItem || typeof rawItem !== "object") return null;
  const productId = normalizeProductId(rawItem.productId ?? rawItem.ProductId);
  if (!productId) return null;
  return {
    id: String(rawItem.id ?? rawItem.Id ?? ""),
    productId,
  };
}

async function getItems() {
  if (!hasToken()) {
    return getLocalItems();
  }

  const response = await axios.get(API.WISHLIST);
  const items = Array.isArray(response?.data) ? response.data : [];
  return items.map(normalizeApiWishlistItem).filter(Boolean);
}

async function addItem(productId) {
  const normalizedProductId = normalizeProductId(productId);
  if (!normalizedProductId) return null;

  if (!hasToken()) {
    const current = getLocalItems();
    if (current.some((item) => item.productId === normalizedProductId)) return { productId: normalizedProductId };
    const next = [{ productId: normalizedProductId, addedAt: new Date().toISOString() }, ...current];
    saveLocalItems(next);
    return { productId: normalizedProductId };
  }

  const numericProductId = Number(normalizedProductId);
  if (!Number.isFinite(numericProductId) || numericProductId <= 0) {
    throw new Error("Invalid product id.");
  }

  const response = await axios.post(API.WISHLIST, {
    productId: numericProductId,
  });
  return normalizeApiWishlistItem(response?.data) || { productId: normalizedProductId };
}

async function removeItem(productId) {
  const normalizedProductId = normalizeProductId(productId);
  if (!normalizedProductId) return;

  if (!hasToken()) {
    const current = getLocalItems();
    saveLocalItems(current.filter((item) => item.productId !== normalizedProductId));
    return;
  }

  await axios.delete(`${API.WISHLIST}/${normalizedProductId}`);
}

function clear() {
  localStorage.removeItem(STORAGE_KEYS.WISHLIST);
}

const wishlistService = {
  getItems,
  addItem,
  removeItem,
  clear,
  getLocalItems,
  saveLocalItems,
};

export default wishlistService;
