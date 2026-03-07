import axios from "../api/axios";
import { API } from "../api/endpoints";
import { STORAGE_KEYS, PROMO_CONFIG } from "../utils/constants";

const COUPON_RULES = {
  FIRSTORDER: { discount: 0.1, message: "10% off applied", singleUse: true },
  [PROMO_CONFIG.ACTIVE_OFFER_CODE]: {
    discount: PROMO_CONFIG.ACTIVE_OFFER_DISCOUNT_PERCENT / 100,
    message: `${PROMO_CONFIG.ACTIVE_OFFER_DISCOUNT_PERCENT}% off applied`,
    singleUse: false,
    minSubtotal: PROMO_CONFIG.ACTIVE_OFFER_MIN_SUBTOTAL,
  },
};

function safeParse(json, fallback) {
  try {
    const parsed = JSON.parse(json);
    return parsed ?? fallback;
  } catch {
    return fallback;
  }
}

function getItemBasedDiscount(itemCount) {
  const count = Number(itemCount) || 0;
  const tiers = [...PROMO_CONFIG.ITEM_COUPON_TIERS].sort((a, b) => Number(b.minItems) - Number(a.minItems));
  const tier = tiers.find((entry) => count >= Number(entry.minItems));
  return tier ? Number(tier.discountPercent) / 100 : 0;
}

function applyCouponLocal(code, context = {}) {
  const normalized = String(code || "").trim().toUpperCase();
  const subtotal = Number(context.subtotal) || 0;
  const itemCount = Number(context.itemCount) || 0;

  if (normalized === PROMO_CONFIG.ITEM_COUPON_CODE) {
    const discount = getItemBasedDiscount(itemCount);
    if (discount <= 0) {
      const minItems = Math.min(...PROMO_CONFIG.ITEM_COUPON_TIERS.map((entry) => Number(entry.minItems) || 0));
      return { valid: false, ok: false, message: `Add at least ${minItems} items to use ${PROMO_CONFIG.ITEM_COUPON_CODE}.` };
    }
    return {
      valid: true,
      ok: true,
      discount,
      message: `${Math.round(discount * 100)}% off applied for ${itemCount} items.`,
    };
  }

  const rule = COUPON_RULES[normalized];
  if (!rule) return { valid: false, ok: false, message: "Invalid coupon" };

  if (rule.minSubtotal && subtotal < rule.minSubtotal) {
    return {
      valid: false,
      ok: false,
      message: `Minimum order value Rs ${rule.minSubtotal} required for this coupon.`,
    };
  }

  const usedRaw = localStorage.getItem(STORAGE_KEYS.COUPON_USAGE);
  const used = usedRaw ? safeParse(usedRaw, []) : [];
  const usedCoupons = Array.isArray(used) ? used : [];

  if (rule.singleUse) {
    if (usedCoupons.includes(normalized)) return { valid: false, ok: false, message: "Coupon already used" };
    usedCoupons.push(normalized);
    localStorage.setItem(STORAGE_KEYS.COUPON_USAGE, JSON.stringify(usedCoupons));
  }

  return { valid: true, ok: true, discount: rule.discount, message: rule.message };
}

async function applyCoupon(code, context = {}) {
  const normalized = String(code || "").trim().toUpperCase();
  if (!normalized) return { valid: false, ok: false, message: "Enter coupon code." };

  const subtotal = Number(context.subtotal) || 0;
  const userId = Number(context.userId) || 0;

  try {
    const response = await axios.post(API.COUPON, {
      code: normalized,
      userId,
      subtotal,
    });

    const payload = response?.data ?? {};
    const discountPercent = Number(payload.discountPercent) || 0;
    const discountAmount = Number(payload.discountAmount) || 0;
    return {
      valid: Boolean(payload.valid ?? true),
      ok: Boolean(payload.valid ?? true),
      discount: discountPercent / 100,
      discountAmount,
      message: String(payload.message || "Coupon applied successfully."),
    };
  } catch (error) {
    if (error?.response) {
      const message =
        typeof error.response.data?.message === "string"
          ? error.response.data.message
          : typeof error.response.data === "string"
          ? error.response.data
          : "Invalid coupon";
      return { valid: false, ok: false, message };
    }
    return applyCouponLocal(normalized, context);
  }
}

export default { applyCoupon };
