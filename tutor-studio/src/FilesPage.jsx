import { PlusOutlined, ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Empty, Modal, Select, Spin } from "antd";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getApiErrorDescription } from "./api/client.js";
import PageHeader from "./components/PageHeader.jsx";
import DocumentCreateDialog from "./documents/DocumentCreateDialog.jsx";
import DocumentFolders from "./documents/DocumentFolders.jsx";
import DocumentPreviewDialog from "./documents/DocumentPreviewDialog.jsx";
import DocumentRow from "./documents/DocumentRow.jsx";
import DocumentUploadDialog from "./documents/DocumentUploadDialog.jsx";
import VectorizeDocumentDialog from "./documents/VectorizeDocumentDialog.jsx";
import useRolePermissions from "./hooks/useRolePermissions.js";
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

export default function FilesPage({ initialUploadOpen = false }) {
    const { canWrite: canManage, canDelete } = useRolePermissions();
    const [documents, setDocuments] = useState([]);
    const [folders, setFolders] = useState([]);
    const [statuses, setStatuses] = useState({});
    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [uploadDocumentTarget, setUploadDocumentTarget] = useState(initialUploadOpen ? null : undefined);
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
                setError(getApiErrorDescription(requestError, "Documents could not be loaded."));
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

    const handleCreateDocument = async (name, rawText, folderId) => {
        const created = await createDocument(name, rawText, folderId);
        const normalized = normalizeDocument(created);
        setDocuments((current) => [normalized, ...current]);
        setStatuses((current) => ({ ...current, [normalized.id]: null }));
        setNotice("Document created.");
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

    const handleMove = async (folderId) => {
        if (!moveTarget) return;
        setError("");
        try {
            await moveDocument(moveTarget.id, folderId || null);
            setDocuments((current) => current.map((document) =>
                document.id === moveTarget.id ? { ...document, folderId: folderId || null } : document));
            setMoveTarget(null);
            setNotice("Document moved.");
        } catch (requestError) {
            setError(getApiErrorDescription(requestError, "The document could not be moved."));
        }
    };

    const folderNames = Object.fromEntries(folders.map((folder) => [folder.id, folder.name]));

    const documentCounts = useMemo(() => {
        const counts = {};
        documents.forEach((doc) => {
            if (doc.folderId) {
                counts[doc.folderId] = (counts[doc.folderId] || 0) + 1;
            }
        });
        return counts;
    }, [documents]);

    const filteredDocuments = useMemo(() => {
        if (selectedFolderId === "all" || !selectedFolderId) return documents;
        if (selectedFolderId === "root") return documents.filter((doc) => !doc.folderId);
        return documents.filter((doc) => doc.folderId === selectedFolderId);
    }, [documents, selectedFolderId]);

    return (
        <main className="workspace-page files-page">
            <div className="workspace-page-inner">
                <PageHeader
                    title="Documents"
                    description={`Upload TXT, Markdown, CSV, JSON, PDF, or DOCX files up to ${defaultMaximumUploadLabel}. Text extraction happens on the server; OCR is not supported.`}
                    extra={[
                        <Button key="refresh" icon={<ReloadOutlined />} onClick={() => loadWorkspace(true)} loading={refreshing}>Refresh</Button>,
                        canManage ? <Button key="upload" type="primary" icon={<PlusOutlined />} onClick={() => setUploadDocumentTarget(null)}>Upload</Button> : null,
                        canManage ? <Button key="create" onClick={() => setCreateDialogOpen(true)}>Create document</Button> : null,
                    ].filter(Boolean)}
                />

                {notice ? <Alert type="success" title={notice} closable onClose={() => setNotice("")} showIcon style={{ marginBottom: 16 }} /> : null}
                {error ? <Alert type="error" title={error} closable onClose={() => setError("")} showIcon style={{ marginBottom: 16 }} /> : null}
                <DocumentFolders
                    folders={folders}
                    selectedFolderId={selectedFolderId}
                    onSelectFolder={setSelectedFolderId}
                    documentCounts={documentCounts}
                    totalCount={documents.length}
                    canManage={canManage}
                    canDelete={canDelete}
                    onFoldersChanged={() => loadWorkspace(true)}
                />

                <div className="files-list-surface">
                    <Spin spinning={loading} description="Loading documents">
                        {!loading && !filteredDocuments.length ? (
                            <Empty description={documents.length ? "No documents match this folder filter" : (canManage ? "No documents yet" : "No documents are available for your account")}>
                                {documents.length && selectedFolderId !== "all" ? (
                                    <Button onClick={() => setSelectedFolderId("all")}>Show all documents</Button>
                                ) : (canManage ? <Button type="primary" onClick={() => setUploadDocumentTarget(null)}>Upload a document</Button> : null)}
                            </Empty>
                        ) : filteredDocuments.map((document) => (
                            <DocumentRow
                                key={document.id}
                                document={document}
                                folderName={folderNames[document.folderId]}
                                status={statuses[document.id]}
                                canManage={canManage}
                                canDelete={canDelete}
                                onPreview={setPreviewDocument}
                                onReplace={setUploadDocumentTarget}
                                onVectorize={setVectorizeDocumentTarget}
                                onDelete={setDeleteTarget}
                                onMove={setMoveTarget}
                            />
                        ))}
                    </Spin>
                </div>
            </div>

            {uploadDocumentTarget !== undefined ? <DocumentUploadDialog document={uploadDocumentTarget} folders={folders} onClose={() => setUploadDocumentTarget(undefined)} onSaved={handleSaved} /> : null}
            {createDialogOpen ? <DocumentCreateDialog folders={folders} onClose={() => setCreateDialogOpen(false)} onCreated={handleCreateDocument} /> : null}
            {previewDocument ? <DocumentPreviewDialog key={previewDocument.id} document={previewDocument} canManage={canManage} onClose={() => setPreviewDocument(null)} onReplace={(document) => { setPreviewDocument(null); setUploadDocumentTarget(document); }} /> : null}
            {vectorizeDocumentTarget ? <VectorizeDocumentDialog key={vectorizeDocumentTarget.id} document={vectorizeDocumentTarget} onClose={() => setVectorizeDocumentTarget(null)} onVectorized={(documentId, status) => { setStatuses((current) => ({ ...current, [documentId]: status })); setNotice(`Vectorized successfully. Created ${status?.chunkCount ?? 0} knowledge chunks.`); }} /> : null}
            <Modal title="Delete document" open={Boolean(deleteTarget)} onCancel={() => setDeleteTarget(null)} onOk={handleDelete} okText="Delete document" okButtonProps={{ danger: true }} confirmLoading={deleting} closable={!deleting} mask={{ closable: !deleting }}>
                <p>Delete <strong>{deleteTarget?.name}</strong>? Its extracted text and knowledge chunks will be permanently removed.</p>
            </Modal>
            <Modal title="Move document" open={Boolean(moveTarget)} onCancel={() => setMoveTarget(null)} onOk={() => handleMove(moveTarget?.folderId)} okText="Move">
                <Select
                    allowClear
                    style={{ width: "100%" }}
                    value={moveTarget?.folderId}
                    placeholder="Root (no folder)"
                    onChange={(folderId) => setMoveTarget((document) => ({ ...document, folderId }))}
                    options={folders.map((folder) => ({ value: folder.id, label: folder.name }))}
                />
            </Modal>
        </main>
    );
}
