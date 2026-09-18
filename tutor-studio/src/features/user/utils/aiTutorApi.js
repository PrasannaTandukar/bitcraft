import { apiFetch } from "../../../api/client.js";

export const listRuntimeTutors = (signal) =>
  apiFetch("/api/runtime/tutors", { signal });

export const askRuntimeTutor = (activationSlug, payload, signal) =>
  apiFetch(`/api/runtime/tutors/${encodeURIComponent(activationSlug)}/ask`, {
    method: "POST",
    json: payload,
    signal,
  });
