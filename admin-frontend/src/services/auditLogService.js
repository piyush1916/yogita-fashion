import apiClient from "../api/axios";
import { API } from "../api/endpoints";

function normalizeAuditLog(rawLog) {
  if (!rawLog || typeof rawLog !== "object") return null;
  const id = rawLog.id ?? rawLog.Id;
  if (id === undefined || id === null) return null;

  return {
    id: String(id),
    actorId: String(rawLog.actorId ?? rawLog.ActorId ?? ""),
    actorEmail: String(rawLog.actorEmail ?? rawLog.ActorEmail ?? "").trim().toLowerCase(),
    actorRole: String(rawLog.actorRole ?? rawLog.ActorRole ?? "").trim(),
    action: String(rawLog.action ?? rawLog.Action ?? "").trim(),
    entityType: String(rawLog.entityType ?? rawLog.EntityType ?? "").trim(),
    entityId: String(rawLog.entityId ?? rawLog.EntityId ?? "").trim(),
    details: String(rawLog.details ?? rawLog.Details ?? "").trim(),
    createdAt: rawLog.createdAt ?? rawLog.CreatedAt ?? "",
  };
}

export async function getAuditLogs({ action = "", entityType = "" } = {}) {
  const response = await apiClient.get(API.AUDIT_LOGS, {
    params: {
      action: action || undefined,
      entityType: entityType || undefined,
    },
  });

  const items = Array.isArray(response?.data) ? response.data : [];
  return items.map(normalizeAuditLog).filter(Boolean);
}
