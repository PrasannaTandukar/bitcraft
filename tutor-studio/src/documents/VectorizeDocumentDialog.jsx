import { Alert, Button, Col, Descriptions, Form, InputNumber, Modal, Row, Select, Spin, Typography } from "antd";
import { useEffect, useMemo, useRef, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { getVectorizationStatus, listEmbeddingModels, vectorizeDocument } from "./documentApi.js";

function formatDate(value) {
  if (!value) return "Never";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "Unknown" : date.toLocaleString();
}

function modelLabel(model) {
  const dimensions = Number.isFinite(Number(model.embeddingDimensions)) ? `${Number(model.embeddingDimensions).toLocaleString()} dimensions` : "Dimensions unavailable";
  return `${model.name} · ${model.providerModelName} · ${dimensions} · ${model.runtimeEngineName || "Runtime"}`;
}

export default function VectorizeDocumentDialog({ document, onClose, onVectorized }) {
  const [status, setStatus] = useState(null);
  const [models, setModels] = useState([]);
  const [modelId, setModelId] = useState();
  const [chunkSize, setChunkSize] = useState(1000);
  const [overlap, setOverlap] = useState(100);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [successCount, setSuccessCount] = useState(null);
  const requestRef = useRef(null);

  useEffect(() => {
    const controller = new AbortController();
    requestRef.current = controller;
    Promise.all([getVectorizationStatus(document.id, controller.signal), listEmbeddingModels(controller.signal)])
      .then(([statusValue, modelValues]) => {
        const usableModels = Array.isArray(modelValues) ? modelValues.filter((model) => model?.isEnabled && model?.isRuntimeEngineEnabled) : [];
        setStatus(statusValue && typeof statusValue === "object" ? statusValue : null);
        setModels(usableModels);
        const existingModelId = statusValue?.embeddingModelId;
        setModelId(usableModels.some((model) => model.id === existingModelId) ? existingModelId : usableModels[0]?.id);
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Vectorization settings could not be loaded."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => {
      controller.abort();
      requestRef.current?.abort();
    };
  }, [document.id]);

  const validation = useMemo(() => {
    if (!Number.isInteger(chunkSize) || chunkSize < 100 || chunkSize > 8000) return "Chunk size must be a whole number from 100 to 8000.";
    if (!Number.isInteger(overlap) || overlap < 0 || overlap >= chunkSize) return "Chunk overlap must be a whole number from 0 up to one less than chunk size.";
    return "";
  }, [chunkSize, overlap]);

  const handleSubmit = async () => {
    if (saving || validation || !modelId || !status?.hasRawText) return;
    setSaving(true);
    setError("");
    setSuccessCount(null);
    const controller = new AbortController();
    requestRef.current = controller;
    try {
      const result = await vectorizeDocument(document.id, { embeddingModelId: modelId, maxChunkLength: chunkSize, chunkOverlap: overlap }, controller.signal);
      const refreshedStatus = await getVectorizationStatus(document.id, controller.signal);
      setStatus(refreshedStatus);
      setSuccessCount(Number(result?.chunkCount) || 0);
      onVectorized(document.id, refreshedStatus);
    } catch (requestError) {
      if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "The embedding service could not process this document. Confirm the model is installed and the runtime is available."));
    } finally {
      if (!controller.signal.aborted) {
        requestRef.current = null;
        setSaving(false);
      }
    }
  };

  return (
    <Modal
      title={status?.isVectorized ? "Re-vectorize file" : "Vectorize file"}
      open
      onCancel={onClose}
      onOk={handleSubmit}
      okText={status?.isVectorized ? "Re-vectorize" : "Vectorize"}
      okButtonProps={{ disabled: saving || loading || Boolean(validation) || !modelId || !status?.hasRawText }}
      confirmLoading={saving}
      closable={!saving}
      mask={{ closable: !saving }}
      width={640}
    >
      <Typography.Paragraph strong>{document.name}</Typography.Paragraph>
      {loading ? <div className="workspace-loading"><Spin description="Loading vectorization settings" /></div> : null}
      {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
      {successCount !== null ? <Alert type="success" title={`Vectorized successfully. Created ${successCount} knowledge chunks.`} showIcon style={{ marginBottom: 16 }} /> : null}
      {!loading && status && !status.hasRawText ? <Alert type="warning" title="This document has no extracted text. Add file content before vectorizing it." showIcon /> : null}
      {!loading && !models.length ? <Alert type="warning" title="No enabled embedding model is available" description="Ask an administrator to configure an embedding model and ensure its runtime model is available." showIcon /> : null}

      {!loading && models.length ? (
        <Form layout="vertical">
          {status?.isVectorized ? <Alert type="info" title={`This replaces ${status.chunkCount} existing chunks after every new embedding has been generated successfully.`} showIcon style={{ marginBottom: 16 }} /> : null}
          <Form.Item label="Embedding model">
            <Select value={modelId} onChange={setModelId} disabled={saving} options={models.map((model) => ({ value: model.id, label: modelLabel(model) }))} />
          </Form.Item>
          <Row gutter={16}>
            <Col span={12}><Form.Item label="Chunk size" validateStatus={validation?.startsWith("Chunk size") ? "error" : undefined}><InputNumber value={chunkSize} onChange={setChunkSize} min={100} max={8000} precision={0} disabled={saving} style={{ width: "100%" }} /></Form.Item></Col>
            <Col span={12}><Form.Item label="Chunk overlap" validateStatus={validation?.startsWith("Chunk overlap") ? "error" : undefined}><InputNumber value={overlap} onChange={setOverlap} min={0} max={7999} precision={0} disabled={saving} style={{ width: "100%" }} /></Form.Item></Col>
          </Row>
          {validation ? <Alert type="error" title={validation} showIcon style={{ marginBottom: 16 }} /> : null}
          <Descriptions size="small" column={2}>
            <Descriptions.Item label="Existing chunks">{status?.chunkCount ?? 0}</Descriptions.Item>
            <Descriptions.Item label="Last vectorized">{formatDate(status?.lastVectorizedUtc)}</Descriptions.Item>
          </Descriptions>
        </Form>
      ) : null}
    </Modal>
  );
}
