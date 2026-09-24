import { apiFetch } from "../../../api/client.js";

const path = (tutorId) => `/api/tutors/${tutorId}/tools`;
export const listTools = (tutorId, signal) => apiFetch(path(tutorId), { signal });
export const getTool = (tutorId, toolId) => apiFetch(`${path(tutorId)}/${toolId}`);
export const getToolCatalog = (tutorId, signal) => apiFetch(`${path(tutorId)}/catalog`, { signal });
export const createTool = (tutorId, values) => apiFetch(path(tutorId), { method: "POST", json: values });
export const updateTool = (tutorId, toolId, values) => apiFetch(`${path(tutorId)}/${toolId}`, { method: "PUT", json: values });
export const deleteTool = (tutorId, toolId) => apiFetch(`${path(tutorId)}/${toolId}`, { method: "DELETE", parse: "none" });
