/**
 * Normalizes a raw document record so `hasRawText`/`rawTextLength` are
 * always present and well-typed, regardless of what the API returned.
 */
export function normalizeDocument(document) {
  const rawTextLength =
    typeof document?.rawText === "string"
      ? document.rawText.length
      : Number(document?.rawTextLength || 0);

  return {
    ...document,
    hasRawText:
      typeof document?.hasRawText === "boolean"
        ? document.hasRawText
        : rawTextLength > 0,
    rawTextLength: Number.isFinite(rawTextLength) ? rawTextLength : 0,
  };
}

/**
 * Maps folder id -> folder name for quick lookup when rendering document rows.
 */
export function buildFolderNameMap(folders) {
  return Object.fromEntries(folders.map((folder) => [folder.id, folder.name]));
}

/**
 * Counts documents per folder id (documents with no folder are excluded).
 */
export function countDocumentsByFolder(documents) {
  const counts = {};
  documents.forEach((doc) => {
    if (doc.folderId) {
      counts[doc.folderId] = (counts[doc.folderId] || 0) + 1;
    }
  });
  return counts;
}

/**
 * Filters documents by the selected folder filter.
 * "all" (or empty) returns everything, "root" returns unfiled documents,
 * any other value returns documents belonging to that folder id.
 */
export function filterDocumentsByFolder(documents, selectedFolderId) {
  if (selectedFolderId === "all" || !selectedFolderId) return documents;
  if (selectedFolderId === "root")
    return documents.filter((doc) => !doc.folderId);
  return documents.filter((doc) => doc.folderId === selectedFolderId);
}
