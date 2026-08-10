import { useCallback, useEffect, useRef, useState } from "react";
import { Alert, Button, Modal, Spinner } from "react-bootstrap";
import { useSelector } from "react-redux";
import { getApiErrorDescription } from "./api/client.js";
import DocumentPreviewDialog from "./documents/DocumentPreviewDialog.jsx";
import DocumentRow from "./documents/DocumentRow.jsx";
import DocumentUploadDialog from "./documents/DocumentUploadDialog.jsx";
import VectorizeDocumentDialog from "./documents/VectorizeDocumentDialog.jsx";
import {
  defaultMaximumUploadLabel,
  deleteDocument,
  getVectorizationStatus,
  listDocumentFolders,
  listDocuments,
} from "./documents/documentApi.js";
import "./documents/FilesPage.css";

function normalizeDocument(document) {
  const rawTextLength = typeof document?.rawText === "string"
    ? document.rawText.length
    : Number(document?.rawTextLength || 0);
  return {
    ...document,
    hasRawText: typeof document?.hasRawText === "boolean"
      ? document.hasRawText
      : rawTextLength > 0,
    rawTextLength: Number.isFinite(rawTextLength) ? rawTextLength : 0,
  };
}

export default function FilesPage() {
  const { roles = [] } = useSelector((state) => state.auth);
  // TODO post-PoC: extract role-check hook when framework auth context is installed.
  const canManage = roles.includes("Editor") || roles.includes("Admin");
  const [documents, setDocuments] = useState([]);
  const [folders, setFolders] = useState([]);
  const [statuses, setStatuses] = useState({});
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [uploadDocumentTarget, setUploadDocumentTarget] = useState(undefined);
  const [previewDocument, setPreviewDocument] = useState(null);
  const [vectorizeDocumentTarget, setVectorizeDocumentTarget] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);
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
        throw new Error("Invalid files response.");
      }
      const nextDocuments = documentValues.filter((item) => item?.id).map(normalizeDocument);
      setDocuments(nextDocuments);
      setFolders(folderValues.filter((item) => item?.id));
      setStatuses({});

      const statusResults = await Promise.allSettled(nextDocuments.map((document) =>
        getVectorizationStatus(document.id, controller.signal)));
      if (!controller.signal.aborted) {
        const nextStatuses = {};
        statusResults.forEach((result, index) => {
          nextStatuses[nextDocuments[index].id] = result.status === "fulfilled" && result.value
            ? result.value
            : { unavailable: true };
        });
        setStatuses(nextStatuses);
      }
    } catch (requestError) {
      if (requestError.name !== "AbortError") {
        setError(getApiErrorDescription(requestError, "Files could not be loaded."));
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

  const handleSaved = (savedDocument, wasReplacement) => {
    const normalized = normalizeDocument(savedDocument);
    setDocuments((current) => wasReplacement
      ? current.map((document) => document.id === normalized.id ? normalized : document)
      : [normalized, ...current]);
    setStatuses((current) => ({ ...current, [normalized.id]: null }));
    setUploadDocumentTarget(undefined);
    setNotice(wasReplacement
      ? "File content was replaced and previous knowledge chunks were removed. You can now vectorize the new text."
      : "File uploaded and text extracted successfully.");
  };

  const handleDelete = async () => {
    if (!deleteTarget || deleting) return;
    setDeleting(true);
    setError("");
    try {
      await deleteDocument(deleteTarget.id);
      setDocuments((current) => current.filter((document) => document.id !== deleteTarget.id));
      setStatuses((current) => {
        const next = { ...current };
        delete next[deleteTarget.id];
        return next;
      });
      setDeleteTarget(null);
      setNotice("File deleted.");
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "The file could not be deleted."));
    } finally {
      setDeleting(false);
    }
  };

  const folderNames = Object.fromEntries(folders.map((folder) => [folder.id, folder.name]));

  return (
    <main className="files-page">
      <div className="files-page-header">
        <div>
          <h1 className="h3 mb-1">Files</h1>
          <p className="files-supported-copy mb-0">
            Upload TXT, Markdown, CSV, JSON, PDF or DOCX files up to {defaultMaximumUploadLabel}. Text extraction happens securely on the server; OCR is not supported.
          </p>
        </div>
        <div className="files-page-actions">
          <Button variant="outline-secondary" onClick={() => loadWorkspace(true)} disabled={refreshing}>
            {refreshing ? <><Spinner size="sm" className="me-2" />Refreshing…</> : "Refresh"}
          </Button>
          {canManage && <Button onClick={() => setUploadDocumentTarget(null)}>Add file</Button>}
        </div>
      </div>

      {notice && <Alert variant="success" dismissible onClose={() => setNotice("")}>{notice}</Alert>}
      {error && <Alert variant="danger" dismissible onClose={() => setError("")}>{error}</Alert>}

      {loading ? (
        <div className="files-empty-state" role="status"><Spinner animation="border" className="me-2" />Loading files…</div>
      ) : documents.length === 0 ? (
        <div className="files-empty-state">
          <h2 className="h5">No files yet</h2>
          <p>{canManage ? "Upload a supported file to extract its text and prepare it for vectorization." : "No documents are available for your account."}</p>
          {canManage && <Button onClick={() => setUploadDocumentTarget(null)}>Add your first file</Button>}
        </div>
      ) : (
        <div className="document-list">
          {documents.map((document) => (
            <DocumentRow
              key={document.id}
              document={document}
              folderName={folderNames[document.folderId]}
              status={statuses[document.id]}
              canManage={canManage}
              onPreview={setPreviewDocument}
              onReplace={setUploadDocumentTarget}
              onVectorize={setVectorizeDocumentTarget}
              onDelete={setDeleteTarget}
            />
          ))}
        </div>
      )}

      {uploadDocumentTarget !== undefined && (
        <DocumentUploadDialog
          document={uploadDocumentTarget}
          folders={folders}
          onClose={() => setUploadDocumentTarget(undefined)}
          onSaved={handleSaved}
        />
      )}

      {previewDocument && (
        <DocumentPreviewDialog
          key={previewDocument.id}
          document={previewDocument}
          canManage={canManage}
          onClose={() => setPreviewDocument(null)}
          onReplace={(document) => {
            setPreviewDocument(null);
            setUploadDocumentTarget(document);
          }}
        />
      )}

      {vectorizeDocumentTarget && (
        <VectorizeDocumentDialog
          key={vectorizeDocumentTarget.id}
          document={vectorizeDocumentTarget}
          onClose={() => setVectorizeDocumentTarget(null)}
          onVectorized={(documentId, status) => {
            setStatuses((current) => ({ ...current, [documentId]: status }));
            setNotice(`Vectorized successfully. Created ${status?.chunkCount ?? 0} knowledge chunks.`);
          }}
        />
      )}

      {deleteTarget && (
        <Modal show onHide={deleting ? undefined : () => setDeleteTarget(null)} backdrop={deleting ? "static" : true} keyboard={!deleting} centered>
          <Modal.Header closeButton={!deleting}><Modal.Title>Delete file</Modal.Title></Modal.Header>
          <Modal.Body>
            Delete <strong>{deleteTarget.name}</strong>? Its extracted text and knowledge chunks will be permanently removed.
          </Modal.Body>
          <Modal.Footer>
            <Button variant="outline-secondary" onClick={() => setDeleteTarget(null)} disabled={deleting}>Cancel</Button>
            <Button variant="danger" onClick={handleDelete} disabled={deleting}>
              {deleting ? <><Spinner size="sm" className="me-2" />Deleting…</> : "Delete file"}
            </Button>
          </Modal.Footer>
        </Modal>
      )}
    </main>
  );
}
