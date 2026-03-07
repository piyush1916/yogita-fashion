import apiClient from "../api/axios";
import { API } from "../api/endpoints";

function toNumber(value, fallback = 0) {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
}

function splitList(value) {
  if (Array.isArray(value)) {
    return value
      .map((item) => String(item || "").trim())
      .filter(Boolean);
  }

  return String(value || "")
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

function normalizeProduct(rawProduct) {
  if (!rawProduct || typeof rawProduct !== "object") return null;
  const id = rawProduct.id ?? rawProduct.Id;
  if (id === undefined || id === null) return null;

  const sizesFromApi = splitList(rawProduct.sizes ?? rawProduct.Sizes);
  const colorsFromApi = splitList(rawProduct.colors ?? rawProduct.Colors);
  const fallbackSize = splitList(rawProduct.size ?? rawProduct.Size);
  const fallbackColor = splitList(rawProduct.color ?? rawProduct.Color);

  const sizes = sizesFromApi.length ? sizesFromApi : fallbackSize;
  const colors = colorsFromApi.length ? colorsFromApi : fallbackColor;
  const price = toNumber(rawProduct.price ?? rawProduct.Price, 0);
  const originalPrice = toNumber(
    rawProduct.originalPrice ??
      rawProduct.OriginalPrice ??
      rawProduct.mrp ??
      rawProduct.Mrp ??
      price,
    price
  );

  return {
    id: String(id),
    name: String(rawProduct.name ?? rawProduct.Name ?? rawProduct.title ?? rawProduct.Title ?? "Untitled Product").trim(),
    description: String(rawProduct.description ?? rawProduct.Description ?? "").trim(),
    category: String(rawProduct.category ?? rawProduct.Category ?? "").trim(),
    price,
    originalPrice,
    size: sizes.join(", "),
    color: colors.join(", "),
    stock: Math.max(0, Math.trunc(toNumber(rawProduct.stock ?? rawProduct.Stock, 0))),
    brand: String(rawProduct.brand ?? rawProduct.Brand ?? "").trim(),
    imageUrl: String(rawProduct.imageUrl ?? rawProduct.ImageUrl ?? rawProduct.image ?? rawProduct.Image ?? "").trim(),
    featuredProduct: Boolean(
      rawProduct.featuredProduct ??
        rawProduct.FeaturedProduct ??
        rawProduct.isBestSeller ??
        rawProduct.IsBestSeller
    ),
    isOutOfStock: Boolean(rawProduct.isOutOfStock ?? rawProduct.IsOutOfStock ?? false),
    isLowStock: Boolean(rawProduct.isLowStock ?? rawProduct.IsLowStock ?? false),
    lowStockThreshold: Math.max(1, toNumber(rawProduct.lowStockThreshold ?? rawProduct.LowStockThreshold, 5)),
  };
}

function toApiPayload(product) {
  const sizes = splitList(product.size ?? product.sizes);
  const colors = splitList(product.color ?? product.colors);
  const price = toNumber(product.price, 0);
  const originalPrice = toNumber(product.originalPrice, price);

  return {
    name: String(product.name || "").trim(),
    description: String(product.description || "").trim(),
    category: String(product.category || "").trim(),
    price,
    originalPrice,
    mrp: originalPrice,
    size: sizes.join(", "),
    color: colors.join(", "),
    sizes,
    colors,
    stock: Math.max(0, Math.trunc(toNumber(product.stock, 0))),
    brand: String(product.brand || "").trim(),
    imageUrl: String(product.imageUrl || "").trim(),
    featuredProduct: Boolean(product.featuredProduct),
    isBestSeller: Boolean(product.featuredProduct),
  };
}

function normalizeAlert(rawAlert) {
  if (!rawAlert || typeof rawAlert !== "object") return null;
  const id = rawAlert.id ?? rawAlert.Id;
  if (id === undefined || id === null) return null;

  return {
    id: String(id),
    productId: String(rawAlert.productId ?? rawAlert.ProductId ?? ""),
    productName: String(rawAlert.productName ?? rawAlert.ProductName ?? "Unknown Product").trim(),
    email: String(rawAlert.email ?? rawAlert.Email ?? "").trim().toLowerCase(),
    whatsAppNumber: String(rawAlert.whatsAppNumber ?? rawAlert.WhatsAppNumber ?? "").trim(),
    createdAt: rawAlert.createdAt ?? rawAlert.CreatedAt ?? "",
    notifiedAt: rawAlert.notifiedAt ?? rawAlert.NotifiedAt ?? null,
  };
}

export async function getProducts() {
  const response = await apiClient.get(API.PRODUCTS);
  const products = Array.isArray(response?.data) ? response.data : [];
  return products.map(normalizeProduct).filter(Boolean);
}

export async function getProductById(id) {
  const response = await apiClient.get(`${API.PRODUCTS}/${id}`);
  return normalizeProduct(response?.data);
}

export async function createProduct(payload) {
  const response = await apiClient.post(API.PRODUCTS, toApiPayload(payload));
  return normalizeProduct(response?.data);
}

export async function updateProduct(id, payload) {
  const response = await apiClient.put(`${API.PRODUCTS}/${id}`, toApiPayload(payload));
  return normalizeProduct(response?.data);
}

export async function deleteProduct(id) {
  await apiClient.delete(`${API.PRODUCTS}/${id}`);
}

export async function getStockAlerts({ pendingOnly = false } = {}) {
  const response = await apiClient.get(API.PRODUCT_STOCK_ALERTS, {
    params: { pendingOnly },
  });
  const alerts = Array.isArray(response?.data) ? response.data : [];
  return alerts.map(normalizeAlert).filter(Boolean);
}

export async function notifyStockAlerts(productId) {
  const response = await apiClient.post(`${API.PRODUCTS}/${productId}/stock-alerts/notify`);
  return response?.data;
}

export async function getLowStockProducts() {
  const response = await apiClient.get(API.PRODUCT_LOW_STOCK);
  const products = Array.isArray(response?.data) ? response.data : [];
  return products.map(normalizeProduct).filter(Boolean);
}

export async function uploadProductImage(file) {
  const formData = new FormData();
  formData.append("file", file);
  const response = await apiClient.post(API.PRODUCT_UPLOAD, formData, {
    headers: { "Content-Type": "multipart/form-data" },
  });

  return {
    url: String(response?.data?.url ?? ""),
    relativeUrl: String(response?.data?.relativeUrl ?? ""),
    fileName: String(response?.data?.fileName ?? ""),
    size: toNumber(response?.data?.size, 0),
  };
}
