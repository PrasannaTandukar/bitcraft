import { apiFetch } from "../../../api/client.js";

export const listRuntimeTutors = (signal) =>
  apiFetch("/api/runtime/tutors", { signal });

export const askRuntimeTutor = (activationSlug, payload, signal) =>
  apiFetch(`/api/runtime/tutors/${encodeURIComponent(activationSlug)}/ask`, {
    method: "POST",
    json: payload,
    signal,
  });

export const getChatAttachmentOptions = (signal) =>
  apiFetch("/api/runtime/attachments/options", { signal });

export const uploadChatImage = (file) => {
  const body = new FormData();
  body.append("file", file);
  return apiFetch("/api/runtime/attachments/images", { method: "POST", body });
};

export const getChatImage = (id, signal) =>
  apiFetch(`/api/runtime/attachments/${encodeURIComponent(id)}`, { parse: "blob", signal });

export const deleteChatImage = (id) =>
  apiFetch(`/api/runtime/attachments/${encodeURIComponent(id)}`, { method: "DELETE", parse: "none" });
