// FILE: src/services/cartService.js

const CART_KEY = "yf_cart_items_v1";
const COUPON_KEY = "yf_cart_coupon_v1";
const DISCOUNT_KEY = "yf_cart_discount_v1";

function safeParse(json, fallback) {
  try {
    const parsed = JSON.parse(json);
    return parsed ?? fallback;
  } catch {
    return fallback;
  }
}

const cartService = {
  // Items
  getCartItems() {
    const raw = localStorage.getItem(CART_KEY);
    const items = raw ? safeParse(raw, []) : [];
    return Array.isArray(items) ? items : [];
  },

  saveCartItems(items) {
    localStorage.setItem(CART_KEY, JSON.stringify(Array.isArray(items) ? items : []));
  },

  // Coupon
  getCoupon() {
    return localStorage.getItem(COUPON_KEY) || "";
  },

  saveCoupon(code) {
    localStorage.setItem(COUPON_KEY, (code || "").toString());
  },

  // Discount
  getDiscount() {
    const raw = localStorage.getItem(DISCOUNT_KEY);
    const num = raw ? Number(raw) : 0;
    return Number.isFinite(num) ? num : 0;
  },

  saveDiscount(amount) {
    const num = Number(amount);
    localStorage.setItem(DISCOUNT_KEY, Number.isFinite(num) ? String(num) : "0");
  },

  clearAll() {
    localStorage.removeItem(CART_KEY);
    localStorage.removeItem(COUPON_KEY);
    localStorage.removeItem(DISCOUNT_KEY);
  },
};

export default cartService;