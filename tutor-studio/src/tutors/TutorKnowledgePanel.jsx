import { FolderOpenOutlined, LinkOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Empty, Flex, Popconfirm, Select, Spin, Tag, Typography } from "antd";
import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getApiErrorDescription } from "../api/client.js";
import { getVectorizationStatus, listDocumentFolders, listDocuments } from "../documents/documentApi.js";
import {
  attachTutorDocument,
  attachTutorDocumentFolder,
  detachTutorDocument,
  detachTutorDocumentFolder,
  listTutorDocumentFolders,
  listTutorDocuments,
} from "./tutorApi.js";

export default function TutorKnowledgePanel({ tutorId, canManage }) {
  const navigate = useNavigate();
  const [documents, setDocuments] = useState([]);
  const [folders, setFolders] = useState([]);
  const [attachedDocuments, setAttachedDocuments] = useState([]);
  const [attachedFolders, setAttachedFolders] = useState([]);
  const [selectedDocumentId, setSelectedDocumentId] = useState();
  const [selectedFolderId, setSelectedFolderId] = useState();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [reload, setReload] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      listDocuments(controller.signal),
      listDocumentFolders(controller.signal),
      listTutorDocuments(tutorId, controller.signal),
      listTutorDocumentFolders(tutorId, controller.signal),
    ])
      .then(async ([loadedDocuments, loadedFolders, loadedAttachedDocuments, loadedAttachedFolders]) => {
        const docs = loadedDocuments ?? [];
        const statuses = canManage
          ? await Promise.all(docs.map(async (document) => {
              try { return [document.id, await getVectorizationStatus(document.id, controller.signal)]; }
              catch { return [document.id, null]; }
            }))
          : [];
        const byId = new Map(statuses);
        setDocuments(docs.map((document) => ({ ...document, vectorization: byId.get(document.id) ?? null })));
        setFolders(loadedFolders ?? []);
        setAttachedDocuments(loadedAttachedDocuments ?? []);
        setAttachedFolders(loadedAttachedFolders ?? []);
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load tutor knowledge."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [canManage, reload, tutorId]);

  const availableDocuments = useMemo(() => documents.filter((document) => attachedDocuments.every((attached) => attached.documentId !== document.id)), [attachedDocuments, documents]);
  const availableFolders = useMemo(() => folders.filter((folder) => attachedFolders.every((attached) => attached.folderId !== folder.id)), [attachedFolders, folders]);
  const selectedDocument = documents.find((document) => document.id === selectedDocumentId);
  const effectiveDocumentIds = useMemo(() => {
    const effectiveFolderIds = new Set(attachedFolders.map((folder) => folder.folderId));
    let addedDescendant = true;
    while (addedDescendant) {
      addedDescendant = false;
      folders.forEach((folder) => {
        if (folder.parentFolderId && effectiveFolderIds.has(folder.parentFolderId) && !effectiveFolderIds.has(folder.id)) {
          effectiveFolderIds.add(folder.id);
          addedDescendant = true;
        }
      });
    }
    return new Set([
      ...attachedDocuments.map((document) => document.documentId),
      ...documents.filter((document) => effectiveFolderIds.has(document.folderId)).map((document) => document.id),
    ]);
  }, [attachedDocuments, attachedFolders, documents, folders]);

  const runMutation = async (operation) => {
    if (saving) return;
    setSaving(true);
    setError("");
    try {
      await operation();
      setSelectedDocumentId(undefined);
      setSelectedFolderId(undefined);
      setLoading(true);
      setReload((value) => value + 1);
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not update tutor knowledge."));
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <div className="workspace-loading"><Spin description="Loading tutor knowledge" /></div>;

  return (
    <div className="tutor-panel-stack">
      <Flex justify="space-between" align="flex-start" gap={16} wrap>
        <div>
          <Typography.Title level={4}>Knowledge</Typography.Title>
          <Typography.Paragraph type="secondary">Attach retrieval-ready documents and folders to this tutor.</Typography.Paragraph>
        </div>
        <Button icon={<FolderOpenOutlined />} onClick={() => navigate("/files")}>Manage files</Button>
      </Flex>

      {error ? <Alert type="error" title={error} showIcon /> : null}
      <Alert type="info" showIcon title={`The next activation will capture ${effectiveDocumentIds.size} effective document${effectiveDocumentIds.size === 1 ? "" : "s"}.`} description="Knowledge changes do not mutate the currently active snapshot." />

      {canManage ? (
        <Card title="Attach a document">
          <Flex gap={8} align="flex-start" wrap>
            <Select
              value={selectedDocumentId}
              onChange={setSelectedDocumentId}
              disabled={saving}
              placeholder="Select a source document"
              style={{ flex: "1 1 320px" }}
              options={availableDocuments.map((document) => ({
                value: document.id,
                label: `${document.name} · ${document.vectorization?.isVectorized && !document.vectorization?.isStale ? "Vectorized" : "Not ready"}`,
              }))}
            />
            <Button type="primary" icon={<LinkOutlined />} loading={saving} disabled={!selectedDocumentId || !selectedDocument?.vectorization?.isVectorized || selectedDocument?.vectorization?.isStale} onClick={() => runMutation(() => attachTutorDocument(tutorId, selectedDocumentId))}>Attach</Button>
          </Flex>
          {selectedDocument && (!selectedDocument.vectorization?.isVectorized || selectedDocument.vectorization?.isStale) ? <Alert type="warning" title="Vectorize this document in Files before attaching it." showIcon style={{ marginTop: 16 }} /> : null}
        </Card>
      ) : null}

      <Card title="Attached documents">
        {attachedDocuments.length ? (
          <div className="tutor-resource-list">
            {attachedDocuments.map((document) => (
              <Flex className="tutor-resource-row" key={document.documentId} align="center" gap={12}>
                <div className="tutor-resource-copy"><Typography.Text strong>{document.documentName}</Typography.Text><Typography.Text type="secondary">{document.embeddingModelName || "No embedding model"}{document.legacyCompatibility ? " · Legacy compatibility link" : ""}</Typography.Text></div>
                <Tag color={document.isVectorized ? "success" : "warning"}>{document.isVectorized ? "Vectorized" : "Not ready"}</Tag>
                {canManage ? <Popconfirm title="Detach this document?" onConfirm={() => runMutation(() => detachTutorDocument(tutorId, document.documentId))}><Button danger size="small" disabled={saving}>Detach</Button></Popconfirm> : null}
              </Flex>
            ))}
          </div>
        ) : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No documents are attached directly" />}
      </Card>

      <Card title="Attached folders">
        {canManage ? (
          <Flex gap={8} wrap style={{ marginBottom: 16 }}>
            <Select value={selectedFolderId} onChange={setSelectedFolderId} disabled={saving} placeholder="Select a folder" style={{ flex: "1 1 280px" }} options={availableFolders.map((folder) => ({ value: folder.id, label: folder.name }))} />
            <Button type="primary" loading={saving} disabled={!selectedFolderId} onClick={() => runMutation(() => attachTutorDocumentFolder(tutorId, selectedFolderId))}>Attach folder</Button>
          </Flex>
        ) : null}
        {attachedFolders.length ? (
          <div className="tutor-resource-list">
            {attachedFolders.map((folder) => (
              <Flex className="tutor-resource-row" key={folder.folderId} align="center" gap={12}>
                <FolderOpenOutlined />
                <Typography.Text strong className="tutor-resource-copy">{folder.folderName}</Typography.Text>
                {canManage ? <Popconfirm title="Detach this folder?" onConfirm={() => runMutation(() => detachTutorDocumentFolder(tutorId, folder.folderId))}><Button danger size="small" disabled={saving}>Detach</Button></Popconfirm> : null}
              </Flex>
            ))}
          </div>
        ) : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No folders are attached" />}
      </Card>
    </div>
  );
}
