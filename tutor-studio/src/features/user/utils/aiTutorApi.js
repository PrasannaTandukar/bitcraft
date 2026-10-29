import { apiFetch, apiSse } from "../../../api/client.js";

export const listRuntimeTutors = (signal) =>
  apiFetch("/api/runtime/tutors", { signal });

export const listRuntimeTutorReadiness = (signal) =>
  apiFetch("/api/runtime/tutors/readiness", { signal });

export const askRuntimeTutor = (activationSlug, payload, signal) =>
  apiFetch(`/api/runtime/tutors/${encodeURIComponent(activationSlug)}/ask`, {
    method: "POST",
    json: payload,
    signal,
  });

export const streamRuntimeTutor = (activationSlug, payload, signal, onEvent) =>
  apiSse(`/api/runtime/tutors/${encodeURIComponent(activationSlug)}/ask/stream`, {
    json: payload, signal, onEvent,
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

const conversationsPath = (slug) => `/api/runtime/tutors/${encodeURIComponent(slug)}/conversations`;
export const listTutorConversations = (slug, signal) => apiFetch(conversationsPath(slug), { signal });
export const createTutorConversation = (slug, signal) => apiFetch(conversationsPath(slug), { method: "POST", signal });
export const getTutorConversation = (slug, id, signal) => apiFetch(`${conversationsPath(slug)}/${encodeURIComponent(id)}`, { signal });
export const deleteTutorConversation = (slug, id) => apiFetch(`${conversationsPath(slug)}/${encodeURIComponent(id)}`, { method: "DELETE", parse: "none" });
