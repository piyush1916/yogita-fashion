export const APP_NAME = "Yogita Fashion";

export const PRODUCT_PAGE_SIZE = 8;

export const SIZE_OPTIONS = ["S", "M", "L", "XL"];

export const COLOR_OPTIONS = [
  "Black",
  "White",
  "Blue",
  "Pink",
  "Green",
  "Maroon",
  "Mustard",
  "Lavender",
  "Red",
  "Beige",
];

export const SORT_OPTIONS = [
  { label: "Newest", value: "newest" },
  { label: "Price Low to High", value: "price-asc" },
  { label: "Price High to Low", value: "price-desc" },
];

export const ORDER_STATUSES = [
  "Pending",
  "Confirmed",
  "Shipped",
  "Delivered",
];

export const STORAGE_KEYS = {
  CART: "yf_cart",
  WISHLIST: "yf_wishlist",
  ADDRESSES: "yf_addresses",
  SUPPORT_REQUESTS: "yf_support_requests",
  ORDERS: "yf_orders",
  COUPON_USAGE: "yf_coupon_usage",
  AUTH_USERS: "yf_auth_users",
  AUTH_SESSION: "yf_auth_session",
  AUTH_TOKEN: "yf_auth_token",
};

export const SUPPORT = {
  WHATSAPP_NUMBER: "917448187062",
  PHONE: "+91 74481 87062",
  EMAIL: "hello@yogitafashion.com",
};

export const PROMO_CONFIG = {
  UPCOMING_SALE_NAME: "Summer Sale",
  UPCOMING_START_DATE_ISO: "2026-03-15T00:00:00+05:30",
  ACTIVE_OFFER_CODE: "SUMMER15",
  ACTIVE_OFFER_DISCOUNT_PERCENT: 15,
  ACTIVE_OFFER_MIN_SUBTOTAL: 2499,
  ITEM_COUPON_CODE: "ITEMBOOST",
  ITEM_COUPON_TIERS: [
    { minItems: 4, discountPercent: 12 },
    { minItems: 2, discountPercent: 8 },
  ],
};
