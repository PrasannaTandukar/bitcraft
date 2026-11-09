import { apiFetch } from "../../../api/client.js";

const DOCUMENTS_PATH = "/api/documents";

export const acceptedDocumentExtensions = ".txt,.md,.csv,.json,.pdf,.docx";
export const defaultMaximumUploadLabel = "10 MB";
export const MAX_BATCH_FILES = 200;

export function listDocuments(signal) {
  return apiFetch(DOCUMENTS_PATH, { signal });
}

export function listDocumentFolders(signal) {
  return apiFetch("/api/document-folders", { signal });
}

export function getDocument(documentId, signal) {
  return apiFetch(`${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}`, {
    signal,
  });
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

export function uploadDocumentsBatch(files, folderId, signal) {
  const formData = new FormData();
  for (const file of files) {
    // Folder uploads carry webkitRelativePath; flatten it into the filename so
    // same-basename files from different subfolders keep distinct document names.
    const relativePath = typeof file.webkitRelativePath === "string" ? file.webkitRelativePath : "";
    if (!relativePath) {
      formData.append("files", file);
      continue;
    }
    const directory = relativePath.slice(0, relativePath.lastIndexOf("/")) || "";
    const name = directory ? `${directory.replaceAll("/", "_")}_${file.name}` : file.name;
    formData.append("files", new File([file], name, { type: file.type }), name);
  }
  if (folderId) formData.append("folderId", folderId);

  return apiFetch(`${DOCUMENTS_PATH}/upload-batch`, {
    method: "POST",
    body: formData,
    signal,
  });
}

export function replaceDocumentContent(documentId, file, signal) {
  const formData = new FormData();
  formData.append("file", file);

  return apiFetch(
    `${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}/content`,
    {
      method: "PUT",
      body: formData,
      signal,
    },
  );
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
  return apiFetch(
    `${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}/vectorization-status`,
    { signal },
  );
}

export function vectorizeDocument(documentId, payload, signal) {
  return apiFetch(
    `${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}/vectorize`,
    {
      method: "POST",
      json: payload,
      signal,
    },
  );
}

export function vectorizeDocumentsBatch(payload, signal) {
  return apiFetch(`${DOCUMENTS_PATH}/vectorize-batch`, {
    method: "POST",
    json: payload,
    signal,
  });
}

export function moveDocument(documentId, folderId) {
  return apiFetch(
    `${DOCUMENTS_PATH}/${encodeURIComponent(documentId)}/folder`,
    {
      method: "PATCH",
      json: { folderId },
    },
  );
}

export function createDocumentFolder(name) {
  return apiFetch("/api/document-folders", {
    method: "POST",
    json: { name },
  });
}

export function renameDocumentFolder(folderId, name) {
  return apiFetch(`/api/document-folders/${encodeURIComponent(folderId)}`, {
    method: "PATCH",
    json: { name },
    parse: "none",
  });
}

export function deleteDocumentFolder(folderId) {
  return apiFetch(`/api/document-folders/${encodeURIComponent(folderId)}`, {
    method: "DELETE",
    parse: "none",
  });
}

export function createDocument(name, rawText, folderId) {
  return apiFetch(DOCUMENTS_PATH, {
    method: "POST",
    json: { name, rawText, folderId: folderId || null },
  });
}
