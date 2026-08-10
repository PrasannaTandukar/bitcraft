import { apiFetch } from "../api/client.js";

export const listTutors = (signal) => apiFetch("/api/tutors", { signal });
export const getTutor = (tutorId, signal) => apiFetch(`/api/tutors/${tutorId}`, { signal });
export const createTutor = (payload) => apiFetch("/api/tutors", { method: "POST", json: payload });

export const listPromptVersions = (tutorId, signal) =>
  apiFetch(`/api/tutors/${tutorId}/prompts`, { signal });
export const createPromptVersion = (tutorId, payload) =>
  apiFetch(`/api/tutors/${tutorId}/prompts`, { method: "POST", json: payload });

export const listTutorRevisions = (tutorId, signal) =>
  apiFetch(`/api/tutors/${tutorId}/revisions`, { signal });
export const createTutorRevision = (tutorId) =>
  apiFetch(`/api/tutors/${tutorId}/revisions`, { method: "POST" });
export const updateTutorRevision = (tutorId, revisionId, payload) =>
  apiFetch(`/api/tutors/${tutorId}/revisions/${revisionId}`, { method: "PUT", json: payload });
export const publishTutorRevision = (tutorId, revisionId) =>
  apiFetch(`/api/tutors/${tutorId}/revisions/${revisionId}/publish`, { method: "POST" });
export const activateTutorRevision = (tutorId, revisionId) =>
  apiFetch(`/api/tutors/${tutorId}/revisions/${revisionId}/activate`, { method: "POST" });
export const archiveTutorRevision = (tutorId, revisionId) =>
  apiFetch(`/api/tutors/${tutorId}/revisions/${revisionId}/archive`, { method: "POST" });
export const discardTutorRevision = (tutorId, revisionId) =>
  apiFetch(`/api/tutors/${tutorId}/revisions/${revisionId}`, { method: "DELETE", parse: "none" });

export const listTutorSnapshots = (tutorId, signal) =>
  apiFetch(`/api/tutors/${tutorId}/snapshots`, { signal });

export const listInteractionModelCatalog = (tutorId, signal) =>
  apiFetch(`/api/model-catalog/interaction-models?tutorId=${encodeURIComponent(tutorId)}`, { signal });
export const listEmbeddingModelCatalog = (tutorId, signal) =>
  apiFetch(`/api/model-catalog/embedding-models?tutorId=${encodeURIComponent(tutorId)}`, { signal });
