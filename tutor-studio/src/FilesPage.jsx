import { PlusOutlined, ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Empty, Modal, Spin } from "antd";
import { useCallback, useEffect, useRef, useState } from "react";
import { useSelector } from "react-redux";
import { getApiErrorDescription } from "./api/client.js";
import PageHeader from "./components/PageHeader.jsx";
import DocumentPreviewDialog from "./documents/DocumentPreviewDialog.jsx";
import DocumentRow from "./documents/DocumentRow.jsx";
import DocumentUploadDialog from "./documents/DocumentUploadDialog.jsx";
import VectorizeDocumentDialog from "./documents/VectorizeDocumentDialog.jsx";
import DocumentFolders from "./documents/DocumentFolders.jsx";
import DocumentCreateDialog from "./documents/DocumentCreateDialog.jsx";
import {
  createDocument,
  defaultMaximumUploadLabel,
  deleteDocument,
  getVectorizationStatus,
  listDocumentFolders,
  listDocuments,
  moveDocument,
} from "./documents/documentApi.js";
import "./documents/FilesPage.css";

function normalizeDocument(document) {
  const rawTextLength = typeof document?.rawText === "string" ? document.rawText.length : Number(document?.rawTextLength || 0);
  return {
    ...document,
    hasRawText: typeof document?.hasRawText === "boolean" ? document.hasRawText : rawTextLength > 0,
    rawTextLength: Number.isFinite(rawTextLength) ? rawTextLength : 0,
  };
}

export default function FilesPage({ defaultView }) {
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
  const [moveTarget, setMoveTarget] = useState(null);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
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
      const [documentValues, folderValues] = await Promise.all([listDocuments(controller.signal), listDocumentFolders(controller.signal)]);
      if (!Array.isArray(documentValues) || !Array.isArray(folderValues)) throw new Error("Invalid files response.");
      const nextDocuments = documentValues.filter((item) => item?.id).map(normalizeDocument);
      setDocuments(nextDocuments);
      setFolders(folderValues.filter((item) => item?.id));
      setStatuses({});

      const statusResults = await Promise.allSettled(nextDocuments.map((document) => getVectorizationStatus(document.id, controller.signal)));
      if (!controller.signal.aborted) {
        const nextStatuses = {};
        statusResults.forEach((result, index) => {
          nextStatuses[nextDocuments[index].id] = result.status === "fulfilled" && result.value ? result.value : { unavailable: true };
        });
        setStatuses(nextStatuses);
      }
    } catch (requestError) {
      if (requestError.name !== "AbortError") {
        setError(getApiErrorDescription(requestError, "Documents could not be loaded."));
      }
      if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Files could not be loaded."));
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

  // Open upload dialog when the upload route is active
  useEffect(() => {
    if (defaultView === "upload" && canManage && uploadDocumentTarget === undefined) {
      setUploadDocumentTarget(null);
    }
  }, [defaultView, canManage, uploadDocumentTarget]);

  const handleSaved = (savedDocument, wasReplacement) => {
    const normalized = normalizeDocument(savedDocument);
    setDocuments((current) => wasReplacement ? current.map((document) => document.id === normalized.id ? normalized : document) : [normalized, ...current]);
    setStatuses((current) => ({ ...current, [normalized.id]: null }));
    setUploadDocumentTarget(undefined);
    setNotice(wasReplacement
      ? "File content was replaced and previous knowledge chunks were removed. You can now vectorize the new text."
      : "File uploaded and text extracted successfully.");
  };

  const handleCreateDocument = async (name, rawText, folderId) => {
    setError("");
    try {
      const created = await createDocument(name, rawText, folderId);
      const normalized = normalizeDocument(created);
      setDocuments((current) => [normalized, ...current]);
      setStatuses((current) => ({ ...current, [normalized.id]: null }));
      setCreateDialogOpen(false);
      setNotice("Document created.");
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "The document could not be created."));
    }
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
      setNotice("Document deleted.");
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "The document could not be deleted."));
    } finally {
      setDeleting(false);
    }
  };

  const handleMove = async (documentId, folderId) => {
    setError("");
    try {
      await moveDocument(documentId, folderId || null);
      setDocuments((current) => current.map((document) =>
        document.id === documentId ? { ...document, folderId: folderId || null } : document
      ));
      setMoveTarget(null);
      setNotice("Document moved.");
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "The document could not be moved."));
    }
  };

  const handleFoldersChanged = () => {
    loadWorkspace(true);
  };

  const folderNames = Object.fromEntries(folders.map((folder) => [folder.id, folder.name]));

  return (
    <main className="workspace-page files-page">
      <div className="workspace-page-inner">
        <PageHeader
          title="Documents"
          description={`Upload TXT, Markdown, CSV, JSON, PDF, or DOCX files up to ${defaultMaximumUploadLabel}. Text extraction happens on the server; OCR is not supported.`}
          extra={[
            <Button key="refresh" icon={<ReloadOutlined />} onClick={() => loadWorkspace(true)} loading={refreshing}>Refresh</Button>,
            canManage ? <Button key="add" type="primary" icon={<PlusOutlined />} onClick={() => setUploadDocumentTarget(null)}>Add file</Button> : null,
            { canManage && <Button variant="outline-primary" onClick={() => setCreateDialogOpen(true)}>Create document</Button>}
          ].filter(Boolean)}
        />

        {notice && <Alert variant="success" dismissible onClose={() => setNotice("")}>{notice}</Alert>}
        {error && <Alert variant="danger" dismissible onClose={() => setError("")}>{error}</Alert>}

        {canManage && (
          <DocumentFolders
            folders={folders}
            loading={loading}
            canManage={canManage}
            onFoldersChanged={handleFoldersChanged}
          />
        )}

        {loading ? (
          <div className="files-empty-state" role="status"><Spinner animation="border" className="me-2" />Loading documents…</div>
        ) : documents.length === 0 ? (
          <div className="files-empty-state">
            <h2 className="h5">No documents yet</h2>
            <p>{canManage ? "Upload a supported file to extract its text and prepare it for vectorization." : "No documents are available for your account."}</p>
            {canManage && <Button onClick={() => setUploadDocumentTarget(null)}>Add your first document</Button>}
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
                onMove={setMoveTarget}
              />
            ))}
            {notice ? <Alert type="success" title={notice} closable onClose={() => setNotice("")} showIcon style={{ marginBottom: 16 }} /> : null}
            {error ? <Alert type="error" title={error} closable onClose={() => setError("")} showIcon style={{ marginBottom: 16 }} /> : null}

            <div className="files-list-surface">
              <Spin spinning={loading} description="Loading files">
                {!loading && !documents.length ? (
                  <Empty description={canManage ? "No files yet" : "No documents are available for your account"}>
                    {canManage ? <Button type="primary" onClick={() => setUploadDocumentTarget(null)}>Add your first file</Button> : null}
                  </Empty>
                ) : documents.map((document) => (
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
              </Spin>
            </div>
          </div>

      {uploadDocumentTarget !== undefined ? <DocumentUploadDialog document={uploadDocumentTarget} folders={folders} onClose={() => setUploadDocumentTarget(undefined)} onSaved={handleSaved} /> : null}
        {previewDocument ? <DocumentPreviewDialog key={previewDocument.id} document={previewDocument} canManage={canManage} onClose={() => setPreviewDocument(null)} onReplace={(document) => { setPreviewDocument(null); setUploadDocumentTarget(document); }} /> : null}
        {vectorizeDocumentTarget ? <VectorizeDocumentDialog key={vectorizeDocumentTarget.id} document={vectorizeDocumentTarget} onClose={() => setVectorizeDocumentTarget(null)} onVectorized={(documentId, status) => { setStatuses((current) => ({ ...current, [documentId]: status })); setNotice(`Vectorized successfully. Created ${status?.chunkCount ?? 0} knowledge chunks.`); }} /> : null}

        <Modal
          title="Delete file"
          open={Boolean(deleteTarget)}
          onCancel={() => setDeleteTarget(null)}
          onOk={handleDelete}
          okText="Delete file"
          okButtonProps={{ danger: true }}
          confirmLoading={deleting}
          closable={!deleting}
          mask={{ closable: !deleting }}
        >
          <p>Delete <strong>{deleteTarget?.name}</strong>? Its extracted text and knowledge chunks will be permanently removed.</p>
        </Modal>
    </main>
  );
}
