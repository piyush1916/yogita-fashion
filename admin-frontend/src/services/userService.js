import apiClient from "../api/axios";
import { API } from "../api/endpoints";

function normalizeUser(rawUser) {
  if (!rawUser || typeof rawUser !== "object") return null;
  const id = rawUser.id ?? rawUser.Id;
  if (id === undefined || id === null) return null;

  return {
    id: String(id),
    name: String(rawUser.name ?? rawUser.Name ?? "").trim(),
    email: String(rawUser.email ?? rawUser.Email ?? "")
      .trim()
      .toLowerCase(),
    phone: String(rawUser.phone ?? rawUser.Phone ?? "").trim(),
    city: String(rawUser.city ?? rawUser.City ?? "").trim(),
    role: String(rawUser.role ?? rawUser.Role ?? "Customer").trim(),
    createdAt: rawUser.createdAt ?? rawUser.CreatedAt ?? "",
  };
}

export async function getUsers() {
  const response = await apiClient.get(API.USERS);
  const users = Array.isArray(response?.data) ? response.data : [];
  return users.map(normalizeUser).filter(Boolean);
}
