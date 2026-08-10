import { apiFetch } from "../api/client.js";

const DOCUMENTS_PATH = "/api/documents";

export const acceptedDocumentExtensions = ".txt,.md,.csv,.json,.pdf,.docx";
export const defaultMaximumUploadLabel = "10 MB";

export function listDocuments(signal) {
  return apiFetch(DOCUMENTS_PATH, { signal });
}

export function listDocumentFolders(signal) {
  return apiFetch("/api/document-folders", { signal });
}

export function getDocument(documentId, signal) {
  return apiFetch(`${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}`, { signal });
}

export function uploadDocument(file, folderId, signal) {
  const formData = new FormData();
  formData.append("file", file);
  if (folderId) formData.append("folderId", folderId);

  return apiFetch(`${DOCUMENTS_PATH}/upload`, {
    method: "POST",
    body: formData,
    signal,
  });
}

export function replaceDocumentContent(documentId, file, signal) {
  const formData = new FormData();
  formData.append("file", file);

  return apiFetch(`${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}/content`, {
    method: "PUT",
    body: formData,
    signal,
  });
}

export function deleteDocument(documentId) {
  return apiFetch(`${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}`, {
    method: "DELETE",
    parse: "none",
  });
}

export function listEmbeddingModels(signal) {
  return apiFetch("/api/model-catalog/embedding-models", { signal });
}

export function getVectorizationStatus(documentId, signal) {
  return apiFetch(`${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}/vectorization-status`, { signal });
}

export function vectorizeDocument(documentId, payload, signal) {
  return apiFetch(`${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}/vectorize`, {
    method: "POST",
    json: payload,
    signal,
  });
}
