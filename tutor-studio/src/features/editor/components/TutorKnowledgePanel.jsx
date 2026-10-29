import { FolderOpenOutlined, LinkOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Empty, Flex, Popconfirm, Select, Spin, Tag, Typography } from "antd";
import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getApiErrorDescription } from "../../../api/client.js";
import { listDocumentFolders, listDocuments } from "../utils/documentApi.js";
import {
  attachTutorDocument,
  attachTutorDocumentFolder,
  detachTutorDocument,
  detachTutorDocumentFolder,
  getTutorKnowledgeOverview,
  captureActiveRevisionKnowledgeSnapshot,
} from "../hooks/tutorApi.js";

export default function TutorKnowledgePanel({ tutorId, canManage }) {
  const navigate = useNavigate();
  const [documents, setDocuments] = useState([]);
  const [folders, setFolders] = useState([]);
  const [attachedDocuments, setAttachedDocuments] = useState([]);
  const [attachedFolders, setAttachedFolders] = useState([]);
  const [overview, setOverview] = useState(null);
  const [selectedDocumentId, setSelectedDocumentId] = useState();
  const [selectedFolderId, setSelectedFolderId] = useState();
  const [loading, setLoading] = useState(true);
  const [dataLoaded, setDataLoaded] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [errorDetail, setErrorDetail] = useState("");
  const [reload, setReload] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    const trackEndpoint = (path, request) => request.catch((requestError) => {
      requestError.knowledgeEndpoint = path;
      throw requestError;
    });
    Promise.all([
      trackEndpoint("GET /api/documents", listDocuments(controller.signal)),
      trackEndpoint("GET /api/document-folders", listDocumentFolders(controller.signal)),
      trackEndpoint(`GET /api/tutors/${tutorId}/knowledge-overview`, getTutorKnowledgeOverview(tutorId, controller.signal)),
    ])
      .then(([loadedDocuments, loadedFolders, loadedOverview]) => {
        const docs = loadedDocuments ?? [];
        setDocuments(docs);
        setFolders(loadedFolders ?? []);
        setAttachedDocuments(loadedOverview?.directDocuments ?? []);
        setAttachedFolders(loadedOverview?.folders ?? []);
        setOverview(loadedOverview);
        setError("");
        setDataLoaded(true);
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") {
          setError(getApiErrorDescription(requestError, "Could not load tutor knowledge."));
          const endpoint = requestError.knowledgeEndpoint;
          setErrorDetail(endpoint ? `Failed request: ${endpoint}${requestError.status ? ` (HTTP ${requestError.status})` : ""}.` : "");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [canManage, reload, tutorId]);

  const availableDocuments = useMemo(() => documents.filter((document) => attachedDocuments.every((attached) => attached.documentId !== document.id)), [attachedDocuments, documents]);
  const availableFolders = useMemo(() => folders.filter((folder) => attachedFolders.every((attached) => attached.folderId !== folder.id)), [attachedFolders, folders]);
  const selectedDocument = documents.find((document) => document.id === selectedDocumentId);
  const effectiveDocuments = overview?.effectiveDocuments ?? [];

  const runMutation = async (operation) => {
    if (saving) return;
    setSaving(true);
      setError("");
      setErrorDetail("");
    try {
      await operation();
      setSelectedDocumentId(undefined);
      setSelectedFolderId(undefined);
      setLoading(true);
      setDataLoaded(false);
      setReload((value) => value + 1);
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not update tutor knowledge."));
    } finally {
      setSaving(false);
    }
  };

  const captureSnapshot = () => runMutation(() => captureActiveRevisionKnowledgeSnapshot(tutorId));

  const retryLoad = () => {
    setError("");
    setErrorDetail("");
    setLoading(true);
    setDataLoaded(false);
    setReload((value) => value + 1);
  };

  if (loading) return <div className="workspace-loading"><Spin description="Loading tutor knowledge" /></div>;

  if (!dataLoaded) return (
    <div className="tutor-panel-stack">
      <Typography.Title level={4}>Knowledge</Typography.Title>
      <Alert
        type="error"
        showIcon
        title={error || "Could not load tutor knowledge."}
        description={<>{errorDetail ? <><Typography.Text>{errorDetail}</Typography.Text><br /></> : null}The current attachment state could not be determined. No attachment or effective-document counts are available.</>}
        action={<Button size="small" onClick={retryLoad}>Retry</Button>}
      />
    </div>
  );

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
      <Alert type="info" showIcon title={`The next activation will capture ${overview.effectiveDocumentCount} effective document${overview.effectiveDocumentCount === 1 ? "" : "s"}.`} description="Knowledge changes do not mutate the currently active snapshot." />
      {effectiveDocuments.length ? <Card title="Effective knowledge">
        <div className="tutor-resource-list">{effectiveDocuments.map((document) => <Flex className="tutor-resource-row" key={document.documentId} align="center" gap={12}>
          <div className="tutor-resource-copy"><Typography.Text strong>{document.documentName}</Typography.Text><Typography.Text type="secondary">{document.attachedThrough}{document.isVectorized ? ` · ${document.chunkCount} chunks` : ""}</Typography.Text></div>
          <Tag color={document.readyForSnapshot ? "success" : "warning"}>{document.readinessStatus === "Ready" ? "Vectorized" : document.readinessStatus === "NotVectorized" ? "Not vectorized" : document.readinessStatus === "Stale" ? "Stale" : "Incompatible"}</Tag>
          {!document.readyForSnapshot ? <Button size="small" onClick={() => navigate("/files")}>Open Files</Button> : null}
        </Flex>)}</div>
      </Card> : null}
      {overview.activeRevisionNumber && overview.effectiveDocumentCount > 0 && effectiveDocuments.every((document) => document.readyForSnapshot) && !overview.activeSnapshotMatchesCurrentKnowledge ? <Alert type="warning" showIcon title="Knowledge is ready, but the active snapshot does not include the current documents." description={canManage ? <Button type="primary" size="small" loading={saving} onClick={captureSnapshot}>Capture new snapshot</Button> : "Ask an editor to capture a new snapshot."} /> : null}

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
                label: `${document.name} · ${!document.vectorization || document.vectorization.unavailable ? "Status check failed" : document.vectorization.isVectorized && !document.vectorization.isStale ? "Vectorized" : "Not vectorized"}`,
              }))}
            />
            <Button type="primary" icon={<LinkOutlined />} loading={saving} disabled={!selectedDocumentId || !selectedDocument?.vectorization || selectedDocument?.vectorization?.unavailable || !selectedDocument?.vectorization?.isVectorized || selectedDocument?.vectorization?.isStale} onClick={() => runMutation(() => attachTutorDocument(tutorId, selectedDocumentId))}>Attach</Button>
          </Flex>
          {selectedDocument && (!selectedDocument.vectorization || selectedDocument.vectorization.unavailable) ? <Alert type="error" title="Vectorization status could not be checked. Retry from Files before attaching this document." showIcon style={{ marginTop: 16 }} /> : null}
          {selectedDocument && !selectedDocument.vectorization?.unavailable && (!selectedDocument.vectorization?.isVectorized || selectedDocument.vectorization?.isStale) ? <Alert type="warning" title="Vectorize this document in Files before attaching it." showIcon style={{ marginTop: 16 }} /> : null}
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
