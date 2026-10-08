import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getApiErrorDescription } from "../../../api/client.js";
import {
  createDocument,
  deleteDocument,
  listDocumentFolders,
  listDocuments,
  moveDocument,
} from "../utils/documentApi.js";
import {
  buildFolderNameMap,
  countDocumentsByFolder,
  filterDocumentsByFolder,
  normalizeDocument,
} from "../utils/filesPageUtils.js";

/**
 * Encapsulates all data-fetching, CRUD, and derived state for FilesPage.
 *
 * @param {boolean} initialUploadOpen - whether the upload dialog should be
 *   open on first render (mirrors the page's `initialUploadOpen` prop).
 */
export default function useFilesWorkspace(initialUploadOpen = false) {
  const [documents, setDocuments] = useState([]);
  const [folders, setFolders] = useState([]);
  const [statuses, setStatuses] = useState({});
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [uploadDocumentTarget, setUploadDocumentTarget] = useState(
    initialUploadOpen ? null : undefined,
  );
  const [previewDocument, setPreviewDocument] = useState(null);
  const [vectorizeDocumentTarget, setVectorizeDocumentTarget] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);
  const [moveTarget, setMoveTarget] = useState(null);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [selectedFolderId, setSelectedFolderId] = useState("all");
  const requestRef = useRef(null);

  const loadWorkspace = useCallback(async (isRefresh = false) => {
    requestRef.current?.abort();
    const controller = new AbortController();
    requestRef.current = controller;

    if (isRefresh) setRefreshing(true);
    else setLoading(true);
    setError("");

    try {
      const [documentValues, folderValues] = await Promise.all([
        listDocuments(controller.signal),
        listDocumentFolders(controller.signal),
      ]);
      if (!Array.isArray(documentValues) || !Array.isArray(folderValues)) {
        throw new Error("Invalid documents response.");
      }

      const nextDocuments = documentValues
        .filter((item) => item?.id)
        .map(normalizeDocument);
      setDocuments(nextDocuments);
      setFolders(folderValues.filter((item) => item?.id));
      const nextStatuses = Object.fromEntries(nextDocuments.map((document) => [
        document.id,
        document.vectorization ?? { unavailable: true },
      ]));
      setStatuses(nextStatuses);
    } catch (requestError) {
      if (requestError.name !== "AbortError") {
        setError(
          getApiErrorDescription(
            requestError,
            "Documents could not be loaded.",
          ),
        );
      }
    } finally {
      if (!controller.signal.aborted) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => loadWorkspace(), 0);
    return () => {
      window.clearTimeout(timer);
      requestRef.current?.abort();
    };
  }, [loadWorkspace]);

  const handleSaved = useCallback((savedDocument, wasReplacement) => {
    const normalized = normalizeDocument(savedDocument);
    setDocuments((current) =>
      wasReplacement
        ? current.map((document) =>
            document.id === normalized.id ? normalized : document,
          )
        : [normalized, ...current],
    );
    setStatuses((current) => ({ ...current, [normalized.id]: null }));
    setUploadDocumentTarget(undefined);
    setNotice(
      wasReplacement
        ? "File content was replaced and previous knowledge chunks were removed. You can now vectorize the new text."
        : "File uploaded and text extracted successfully.",
    );
  }, []);

  const handleCreateDocument = useCallback(async (name, rawText, folderId) => {
    const created = await createDocument(name, rawText, folderId);
    const normalized = normalizeDocument(created);
    setDocuments((current) => [normalized, ...current]);
    setStatuses((current) => ({ ...current, [normalized.id]: null }));
    setNotice("Document created.");
  }, []);

  const handleDelete = useCallback(async () => {
    if (!deleteTarget || deleting) return;
    setDeleting(true);
    setError("");
    try {
      await deleteDocument(deleteTarget.id);
      setDocuments((current) =>
        current.filter((document) => document.id !== deleteTarget.id),
      );
      setStatuses((current) => {
        const next = { ...current };
        delete next[deleteTarget.id];
        return next;
      });
      setDeleteTarget(null);
      setNotice("Document deleted.");
    } catch (requestError) {
      setError(
        getApiErrorDescription(
          requestError,
          "The document could not be deleted.",
        ),
      );
    } finally {
      setDeleting(false);
    }
  }, [deleteTarget, deleting]);

  const handleMove = useCallback(
    async (folderId) => {
      if (!moveTarget) return;
      setError("");
      try {
        await moveDocument(moveTarget.id, folderId || null);
        setDocuments((current) =>
          current.map((document) =>
            document.id === moveTarget.id
              ? { ...document, folderId: folderId || null }
              : document,
          ),
        );
        setMoveTarget(null);
        setNotice("Document moved.");
      } catch (requestError) {
        setError(
          getApiErrorDescription(
            requestError,
            "The document could not be moved.",
          ),
        );
      }
    },
    [moveTarget],
  );

  const folderNames = useMemo(() => buildFolderNameMap(folders), [folders]);
  const documentCounts = useMemo(
    () => countDocumentsByFolder(documents),
    [documents],
  );
  const filteredDocuments = useMemo(
    () => filterDocumentsByFolder(documents, selectedFolderId),
    [documents, selectedFolderId],
  );

  return {
    documents,
    folders,
    statuses,
    setStatuses,
    loading,
    refreshing,
    error,
    setError,
    notice,
    setNotice,
    uploadDocumentTarget,
    setUploadDocumentTarget,
    previewDocument,
    setPreviewDocument,
    vectorizeDocumentTarget,
    setVectorizeDocumentTarget,
    deleteTarget,
    setDeleteTarget,
    deleting,
    moveTarget,
    setMoveTarget,
    createDialogOpen,
    setCreateDialogOpen,
    selectedFolderId,
    setSelectedFolderId,
    loadWorkspace,
    handleSaved,
    handleCreateDocument,
    handleDelete,
    handleMove,
    folderNames,
    documentCounts,
    filteredDocuments,
  };
}
