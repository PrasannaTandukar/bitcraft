import { PlusOutlined, ReloadOutlined, ThunderboltOutlined } from "@ant-design/icons";
import { Alert, Button, Empty, Modal, Select, Spin } from "antd";
import { useState } from "react";
import PageHeader from "../../components/PageHeader.jsx";
import DocumentCreateDialog from "../../features/editor/components/DocumentCreateDialog.jsx";
import DocumentFolders from "../../features/editor/components/DocumentFolders.jsx";
import DocumentPreviewDialog from "../../features/editor/components/DocumentPreviewDialog.jsx";
import DocumentRow from "../../features/editor/components/DocumentRow.jsx";
import DocumentUploadDialog from "../../features/editor/components/DocumentUploadDialog.jsx";
import VectorizeDocumentDialog from "../../features/editor/components/VectorizeDocumentDialog.jsx";
import VectorizeFolderDialog from "../../features/editor/components/VectorizeFolderDialog.jsx";
import useRolePermissions from "../../hooks/useRolePermissions.js";
import { defaultMaximumUploadLabel } from "../../features/editor/utils/documentApi.js";
import useFilesWorkspace from "../../features/editor/hooks/useFiles.js";
import "../../styles/FilesPage.css";

export default function FilesPage({ initialUploadOpen = false }) {
    const { canWrite: canManage, canDelete } = useRolePermissions();
    const [vectorizeFolderOpen, setVectorizeFolderOpen] = useState(false);
    const {
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
    } = useFilesWorkspace(initialUploadOpen);
    const unavailableStatusCount = Object.values(statuses).filter((status) => status?.unavailable).length;
    const vectorizeFolderId = selectedFolderId === "root" ? null : (selectedFolderId !== "all" ? selectedFolderId : undefined);
    const vectorizeFolderName = selectedFolderId === "root" ? null : folderNames[selectedFolderId];
    const vectorizeFolderCount = selectedFolderId === "root"
        ? documents.filter((document) => !document.folderId).length
        : (vectorizeFolderId ? (documentCounts[vectorizeFolderId] ?? 0) : 0);

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
                        canManage && vectorizeFolderId !== undefined && vectorizeFolderCount > 0 ? (
                            <Button key="vectorize-folder" icon={<ThunderboltOutlined />} onClick={() => setVectorizeFolderOpen(true)}>Vectorize folder</Button>
                        ) : null,
                    ].filter(Boolean)}
                />

                {notice ? <Alert type="success" title={notice} closable onClose={() => setNotice("")} showIcon style={{ marginBottom: 16 }} /> : null}
                {error ? <Alert type="error" title={error} closable onClose={() => setError("")} showIcon style={{ marginBottom: 16 }} /> : null}
                {unavailableStatusCount > 0 ? <Alert
                    type="warning"
                    title={`Vectorization status could not be checked for ${unavailableStatusCount} document${unavailableStatusCount === 1 ? "" : "s"}.`}
                    action={<Button size="small" onClick={() => loadWorkspace(true)} loading={refreshing}>Retry</Button>}
                    showIcon
                    style={{ marginBottom: 16 }}
                /> : null}
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

            {uploadDocumentTarget !== undefined ? <DocumentUploadDialog document={uploadDocumentTarget} folders={folders} onClose={() => setUploadDocumentTarget(undefined)} onSaved={handleSaved} onRefresh={() => loadWorkspace(true)} /> : null}
            {createDialogOpen ? <DocumentCreateDialog folders={folders} onClose={() => setCreateDialogOpen(false)} onCreated={handleCreateDocument} /> : null}
            {previewDocument ? <DocumentPreviewDialog key={previewDocument.id} document={previewDocument} canManage={canManage} onClose={() => setPreviewDocument(null)} onReplace={(document) => { setPreviewDocument(null); setUploadDocumentTarget(document); }} /> : null}
            {vectorizeDocumentTarget ? <VectorizeDocumentDialog key={vectorizeDocumentTarget.id} document={vectorizeDocumentTarget} onClose={() => setVectorizeDocumentTarget(null)} onVectorized={(documentId, status) => { setStatuses((current) => ({ ...current, [documentId]: status })); setNotice(`Vectorized successfully. Created ${status?.chunkCount ?? 0} knowledge chunks.`); }} /> : null}
            {vectorizeFolderOpen ? <VectorizeFolderDialog folderId={vectorizeFolderId} folderName={vectorizeFolderName} documentCount={vectorizeFolderCount} onClose={() => setVectorizeFolderOpen(false)} onVectorized={() => { loadWorkspace(true); setNotice("Folder vectorization finished."); }} /> : null}
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
