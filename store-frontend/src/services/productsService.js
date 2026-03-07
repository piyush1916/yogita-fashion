import axios from "../api/axios";
import { API } from "../api/endpoints";

function toArray(value) {
  return Array.isArray(value) ? value : [];
}

function normalizeProducts(payload) {
  if (Array.isArray(payload)) return payload;
  if (Array.isArray(payload?.items)) return payload.items;
  if (Array.isArray(payload?.data)) return payload.data;
  return [];
}

function toNumber(value, fallback = 0) {
  const num = Number(value);
  return Number.isFinite(num) ? num : fallback;
}

function createVariants({ sizes, colors, stock }) {
  const safeSizes = sizes.length ? sizes : ["S", "M", "L", "XL"];
  const safeColors = colors.length ? colors : ["Black"];
  const safeStock = Math.max(0, toNumber(stock, 0));

  const variants = [];
  for (const size of safeSizes) {
    for (const color of safeColors) {
      variants.push({ size, color, stock: safeStock });
    }
  }
  return variants;
}

function mapApiProduct(raw) {
  if (!raw || typeof raw !== "object") return null;

  const idValue = raw.id ?? raw.Id;
  if (typeof idValue === "undefined" || idValue === null || idValue === "") return null;

  const title = String(raw.title ?? raw.Title ?? raw.name ?? raw.Name ?? "Product").trim();
  const category = String(raw.category ?? raw.Category ?? "Fashion").trim();
  const price = toNumber(raw.price ?? raw.Price, 0);
  const mrp = toNumber(raw.mrp ?? raw.Mrp, price > 0 ? price : 0);

  const imageUrl = String(raw.imageUrl ?? raw.ImageUrl ?? raw.image ?? raw.Image ?? "").trim();
  const rawImages = toArray(raw.images ?? raw.Images).map((img) => String(img || "").trim()).filter(Boolean);
  const images = rawImages.length ? rawImages : imageUrl ? [imageUrl] : ["https://via.placeholder.com/400x400?text=Product"];

  const sizes = toArray(raw.sizes ?? raw.Sizes).map((size) => String(size || "").trim()).filter(Boolean);
  const colors = toArray(raw.colors ?? raw.Colors).map((color) => String(color || "").trim()).filter(Boolean);

  const rawVariants = toArray(raw.variants ?? raw.Variants);
  const variants = rawVariants.length
    ? rawVariants
        .map((variant) => ({
          size: String(variant?.size ?? variant?.Size ?? "").trim(),
          color: String(variant?.color ?? variant?.Color ?? "").trim(),
          stock: Math.max(0, toNumber(variant?.stock ?? variant?.Stock, 0)),
        }))
        .filter((variant) => variant.size && variant.color)
    : createVariants({ sizes, colors, stock: raw.stock ?? raw.Stock });

  const normalizedSizes = sizes.length ? sizes : [...new Set(variants.map((variant) => variant.size))].filter(Boolean);
  const normalizedColors = colors.length ? colors : [...new Set(variants.map((variant) => variant.color))].filter(Boolean);

  return {
    id: String(idValue),
    title,
    category,
    price,
    mrp,
    stock: Math.max(0, toNumber(raw.stock ?? raw.Stock, 0)),
    images,
    shortDescription: String(raw.shortDescription ?? raw.ShortDescription ?? raw.description ?? raw.Description ?? "").trim(),
    details: {
      material: String(raw?.details?.material ?? raw?.Details?.Material ?? "Cotton blend").trim(),
      fit: String(raw?.details?.fit ?? raw?.Details?.Fit ?? "Regular").trim(),
      style: String(raw?.details?.style ?? raw?.Details?.Style ?? "Casual").trim(),
      sleeve: String(raw?.details?.sleeve ?? raw?.Details?.Sleeve ?? "Half Sleeve").trim(),
      washCare: String(raw?.details?.washCare ?? raw?.Details?.WashCare ?? "Machine wash").trim(),
    },
    isNew: Boolean(raw.isNew ?? raw.IsNew),
    isBestSeller: Boolean(raw.isBestSeller ?? raw.IsBestSeller),
    sizes: normalizedSizes.length ? normalizedSizes : ["S", "M", "L", "XL"],
    colors: normalizedColors.length ? normalizedColors : ["Black"],
    variants,
  };
}

function mapProducts(payload) {
  return normalizeProducts(payload).map(mapApiProduct).filter(Boolean);
}

function applyFilters(items, { search = "", filters = {}, sort = "" } = {}) {
  const q = String(search || "").trim().toLowerCase();
  const category = toArray(filters?.category).map((x) => String(x).toLowerCase());
  const size = toArray(filters?.size).map((x) => String(x).toLowerCase());
  const color = toArray(filters?.color).map((x) => String(x).toLowerCase());
  const minPrice = Number(filters?.price?.min) || 0;
  const maxPrice = Number(filters?.price?.max) || 0;

  let next = toArray(items).filter((product) => {
    const title = String(product?.title || "").toLowerCase();
    const cat = String(product?.category || "").toLowerCase();
    const price = Number(product?.price) || 0;
    const sizes = toArray(product?.sizes).map((x) => String(x).toLowerCase());
    const colors = toArray(product?.colors).map((x) => String(x).toLowerCase());

    if (q && !title.includes(q) && !cat.includes(q)) return false;
    if (category.length && !category.includes(cat)) return false;
    if (size.length && !size.some((value) => sizes.includes(value))) return false;
    if (color.length && !color.some((value) => colors.includes(value))) return false;
    if (minPrice > 0 && price < minPrice) return false;
    if (maxPrice > 0 && price > maxPrice) return false;
    return true;
  });

  if (sort === "price-asc") {
    next = [...next].sort((a, b) => (Number(a?.price) || 0) - (Number(b?.price) || 0));
  } else if (sort === "price-desc") {
    next = [...next].sort((a, b) => (Number(b?.price) || 0) - (Number(a?.price) || 0));
  } else if (sort === "newest") {
    next = [...next].sort((a, b) => {
      const aScore = a?.isNew ? 1 : 0;
      const bScore = b?.isNew ? 1 : 0;
      if (bScore !== aScore) return bScore - aScore;
      return Number(b?.id) - Number(a?.id);
    });
  }

  return next;
}

async function fetchAllProducts() {
  try {
    const res = await axios.get(API.PRODUCTS);
    return mapProducts(res?.data);
  } catch {
    // If API is unavailable, keep catalog empty.
    return [];
  }
}

export async function getProducts() {
  return fetchAllProducts();
}

export async function getProductById(id) {
  const targetId = String(id || "").trim();
  if (!targetId) return null;

  try {
    const res = await axios.get(`${API.PRODUCTS}/${targetId}`);
    const product = mapApiProduct(res?.data);
    if (product) return product;
  } catch {
    // Ignore API error and search in fallback set.
  }

  const all = await fetchAllProducts();
  return all.find((product) => String(product?.id) === targetId) || null;
}

export async function requestStockAlert(productId, payload) {
  const targetId = String(productId || "").trim();
  if (!targetId) throw new Error("Product id is required.");

  const rawWhatsApp = payload?.whatsAppNumber ?? payload?.whatsApp ?? payload?.whatsapp ?? "";
  const safePayload = {
    email: String(payload?.email || "")
      .trim()
      .toLowerCase(),
    whatsAppNumber: String(rawWhatsApp).trim(),
  };

  const response = await axios.post(`${API.PRODUCT_STOCK_ALERTS}/${targetId}/stock-alerts`, safePayload);
  return response?.data;
}

async function list({ page = 1, size = 8, search = "", filters = {}, sort = "" } = {}) {
  const safePage = Math.max(1, Number(page) || 1);
  const safeSize = Math.max(1, Number(size) || 8);

  const all = await fetchAllProducts();
  const filtered = applyFilters(all, { search, filters, sort });

  const start = (safePage - 1) * safeSize;
  const end = start + safeSize;
  return {
    items: filtered.slice(start, end),
    total: filtered.length,
  };
}

async function getById(id) {
  return getProductById(id);
}

const productsService = {
  list,
  getById,
  getProducts,
  getProductById,
  requestStockAlert,
};

export default productsService;
