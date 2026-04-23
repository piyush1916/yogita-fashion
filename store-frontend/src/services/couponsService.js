import axios from "../api/axios";
import { API } from "../api/endpoints";

function parseMessage(error, fallback) {
  if (!error?.response) return fallback;
  if (typeof error.response.data?.message === "string") return error.response.data.message;
  if (typeof error.response.data === "string") return error.response.data;
  return fallback;
}

async function applyCoupon(code, context = {}) {
  const normalized = String(code || "").trim().toUpperCase();
  if (!normalized) {
    return { valid: false, ok: false, message: "Enter coupon code." };
  }

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
      code: normalized,
      discount: discountPercent / 100,
      discountAmount,
      message: String(payload.message || "Coupon applied successfully."),
    };
  } catch (error) {
    return {
      valid: false,
      ok: false,
      code: normalized,
      discount: 0,
      discountAmount: 0,
      message: parseMessage(error, "Unable to validate coupon right now."),
    };
  }
}

export default { applyCoupon };
