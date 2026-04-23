import axios from "../api/axios";
import { API } from "../api/endpoints";

function normalizeText(value) {
  return String(value || "").trim();
}

function normalizeAddress(rawAddress) {
  if (!rawAddress || typeof rawAddress !== "object") return null;
  const id = rawAddress.id ?? rawAddress.Id;
  if (id === undefined || id === null || id === "") return null;

  return {
    id: String(id),
    userId: Number(rawAddress.userId ?? rawAddress.UserId) || 0,
    fullName: normalizeText(rawAddress.fullName ?? rawAddress.FullName),
    phone: normalizeText(rawAddress.phone ?? rawAddress.Phone),
    line1: normalizeText(rawAddress.street ?? rawAddress.Street),
    line2: normalizeText(rawAddress.line2 ?? rawAddress.Line2),
    city: normalizeText(rawAddress.city ?? rawAddress.City),
    state: normalizeText(rawAddress.state ?? rawAddress.State),
    pincode: normalizeText(rawAddress.pincode ?? rawAddress.Pincode),
    landmark: normalizeText(rawAddress.landmark ?? rawAddress.Landmark),
    type: normalizeText(rawAddress.addressType ?? rawAddress.AddressType) || "Home",
    isDefault: Boolean(rawAddress.isDefault ?? rawAddress.IsDefault),
    createdAt: normalizeText(rawAddress.createdAt ?? rawAddress.CreatedAt),
    updatedAt: normalizeText(rawAddress.updatedAt ?? rawAddress.UpdatedAt),
  };
}

function toPayload(form) {
  return {
    fullName: normalizeText(form?.fullName),
    phone: normalizeText(form?.phone),
    street: normalizeText(form?.line1),
    line2: normalizeText(form?.line2),
    city: normalizeText(form?.city),
    state: normalizeText(form?.state),
    pincode: normalizeText(form?.pincode),
    landmark: normalizeText(form?.landmark),
    addressType: normalizeText(form?.type) || "Home",
    isDefault: Boolean(form?.isDefault),
  };
}

async function listByUser() {
  const response = await axios.get(API.ADDRESS);
  const items = Array.isArray(response?.data) ? response.data : [];
  return items.map(normalizeAddress).filter(Boolean);
}

async function createAddress(form) {
  const response = await axios.post(API.ADDRESS, toPayload(form));
  return normalizeAddress(response?.data);
}

async function updateAddress(id, form) {
  const response = await axios.patch(`${API.ADDRESS}/${id}`, toPayload(form));
  return normalizeAddress(response?.data);
}

async function deleteAddress(id) {
  await axios.delete(`${API.ADDRESS}/${id}`);
}

const addressService = {
  listByUser,
  createAddress,
  updateAddress,
  deleteAddress,
};

export default addressService;
