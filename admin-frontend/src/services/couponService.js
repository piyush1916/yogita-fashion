import apiClient from "../api/axios";
import { API } from "../api/endpoints";

function toNumber(value, fallback = 0) {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
}

function toIsoOrNull(value) {
  const raw = String(value ?? "").trim();
  if (!raw) return null;
  const parsed = new Date(raw);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

function normalizeCoupon(rawCoupon) {
  if (!rawCoupon || typeof rawCoupon !== "object") return null;
  const id = rawCoupon.id ?? rawCoupon.Id;
  if (id === undefined || id === null) return null;

  return {
    id: String(id),
    code: String(rawCoupon.code ?? rawCoupon.Code ?? "").trim().toUpperCase(),
    type: String(rawCoupon.type ?? rawCoupon.Type ?? "percent").trim().toLowerCase() === "fixed" ? "fixed" : "percent",
    value: toNumber(rawCoupon.value ?? rawCoupon.Value, 0),
    minOrderAmount: toNumber(rawCoupon.minOrderAmount ?? rawCoupon.MinOrderAmount, 0),
    maxUses: Math.max(0, Math.trunc(toNumber(rawCoupon.maxUses ?? rawCoupon.MaxUses, 0))),
    maxUsesPerUser: Math.max(0, Math.trunc(toNumber(rawCoupon.maxUsesPerUser ?? rawCoupon.MaxUsesPerUser, 1))),
    usedCount: Math.max(0, Math.trunc(toNumber(rawCoupon.usedCount ?? rawCoupon.UsedCount, 0))),
    isActive: Boolean(rawCoupon.isActive ?? rawCoupon.IsActive),
    startAt: rawCoupon.startAt ?? rawCoupon.StartAt ?? "",
    endAt: rawCoupon.endAt ?? rawCoupon.EndAt ?? "",
    createdAt: rawCoupon.createdAt ?? rawCoupon.CreatedAt ?? "",
    updatedAt: rawCoupon.updatedAt ?? rawCoupon.UpdatedAt ?? "",
  };
}

function toPayload(coupon) {
  return {
    code: String(coupon?.code ?? "").trim().toUpperCase(),
    type: String(coupon?.type ?? "percent").trim().toLowerCase() === "fixed" ? "fixed" : "percent",
    value: Math.max(0, toNumber(coupon?.value, 0)),
    minOrderAmount: Math.max(0, toNumber(coupon?.minOrderAmount, 0)),
    maxUses: Math.max(0, Math.trunc(toNumber(coupon?.maxUses, 0))),
    maxUsesPerUser: Math.max(0, Math.trunc(toNumber(coupon?.maxUsesPerUser, 1))),
    isActive: Boolean(coupon?.isActive),
    startAt: toIsoOrNull(coupon?.startAt),
    endAt: toIsoOrNull(coupon?.endAt),
  };
}

export async function getCoupons() {
  const response = await apiClient.get(API.COUPONS);
  const items = Array.isArray(response?.data) ? response.data : [];
  return items.map(normalizeCoupon).filter(Boolean);
}

export async function createCoupon(payload) {
  const response = await apiClient.post(API.COUPONS, toPayload(payload));
  return normalizeCoupon(response?.data);
}

export async function updateCoupon(id, payload) {
  const response = await apiClient.put(`${API.COUPONS}/${id}`, toPayload(payload));
  return normalizeCoupon(response?.data);
}

export async function deleteCoupon(id) {
  await apiClient.delete(`${API.COUPONS}/${id}`);
}
