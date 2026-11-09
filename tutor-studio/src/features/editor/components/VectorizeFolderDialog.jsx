import { Alert, Form, InputNumber, List, Modal, Select, Spin, Typography } from "antd";
import { useEffect, useMemo, useRef, useState } from "react";
import { getApiErrorDescription } from "../../../api/client.js";
import { listEmbeddingModels, vectorizeDocumentsBatch } from "../utils/documentApi.js";

function modelLabel(model) {
  const dimensions = Number.isFinite(Number(model.embeddingDimensions)) ? `${Number(model.embeddingDimensions).toLocaleString()} dimensions` : "Dimensions unavailable";
  return `${model.name} · ${model.providerModelName} · ${dimensions} · ${model.runtimeEngineName || "Runtime"}`;
}

export default function VectorizeFolderDialog({ folderId, folderName, documentCount, onClose, onVectorized }) {
  const [models, setModels] = useState([]);
  const [modelId, setModelId] = useState();
  const [chunkSize, setChunkSize] = useState(1000);
  const [overlap, setOverlap] = useState(100);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [results, setResults] = useState(null);
  const requestRef = useRef(null);

  useEffect(() => {
    const controller = new AbortController();
    requestRef.current = controller;
    listEmbeddingModels(controller.signal)
      .then((modelValues) => {
        const usableModels = Array.isArray(modelValues) ? modelValues.filter((model) => model?.isEnabled && model?.isRuntimeEngineEnabled) : [];
        setModels(usableModels);
        setModelId(usableModels[0]?.id);
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Embedding models could not be loaded."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => {
      controller.abort();
      requestRef.current?.abort();
    };
  }, []);

  const validation = useMemo(() => {
    if (!Number.isInteger(chunkSize) || chunkSize < 100 || chunkSize > 8000) return "Chunk size must be a whole number from 100 to 8000.";
    if (!Number.isInteger(overlap) || overlap < 0 || overlap >= chunkSize) return "Chunk overlap must be a whole number from 0 up to one less than chunk size.";
    return "";
  }, [chunkSize, overlap]);

  const handleSubmit = async () => {
    if (saving || validation || !modelId) return;
    setSaving(true);
    setError("");
    setResults(null);
    const controller = new AbortController();
    requestRef.current = controller;
    try {
      const response = await vectorizeDocumentsBatch(
        { folderId, embeddingModelId: modelId, maxChunkLength: chunkSize, chunkOverlap: overlap },
        controller.signal,
      );
      setResults(Array.isArray(response?.results) ? response.results : []);
      onVectorized?.();
    } catch (requestError) {
      if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "The embedding service could not process this batch. Confirm the model is installed and the runtime is available."));
    } finally {
      if (!controller.signal.aborted) {
        requestRef.current = null;
        setSaving(false);
      }
    }
  };

  const succeeded = results ? results.filter((entry) => Number.isInteger(entry?.chunkCount)).length : 0;

  return (
    <Modal
      title={`Vectorize ${folderName || "root"}`}
      open
      onCancel={onClose}
      onOk={handleSubmit}
      okText="Vectorize folder"
      okButtonProps={{ disabled: saving || loading || Boolean(validation) || !modelId }}
      confirmLoading={saving}
      closable={!saving}
      mask={{ closable: !saving }}
      width={640}
    >
      <Typography.Paragraph type="secondary">
        Rebuilds knowledge chunks for {documentCount} document{documentCount === 1 ? "" : "s"} in {folderName ? `“${folderName}”` : "the root folder"}. A per-document failure does not stop the batch.
      </Typography.Paragraph>
      {loading ? <div className="workspace-loading"><Spin description="Loading embedding models" /></div> : null}
      {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
      {results ? (
        <Alert
          type={succeeded === results.length ? "success" : "warning"}
          title={`Vectorized ${succeeded} of ${results.length} documents.`}
          showIcon
          style={{ marginBottom: 16 }}
        />
      ) : null}
      {!loading && !models.length ? <Alert type="warning" title="No enabled embedding model is available" description="Ask an administrator to configure an embedding model and ensure its runtime model is available." showIcon /> : null}

      {!loading && models.length ? (
        <Form layout="vertical">
          <Form.Item label="Embedding model">
            <Select value={modelId} onChange={setModelId} disabled={saving} options={models.map((model) => ({ value: model.id, label: modelLabel(model) }))} />
          </Form.Item>
          <Form.Item label="Chunk size" validateStatus={validation?.startsWith("Chunk size") ? "error" : undefined}>
            <InputNumber value={chunkSize} onChange={setChunkSize} min={100} max={8000} precision={0} disabled={saving} style={{ width: "100%" }} />
          </Form.Item>
          <Form.Item label="Chunk overlap" validateStatus={validation?.startsWith("Chunk overlap") ? "error" : undefined}>
            <InputNumber value={overlap} onChange={setOverlap} min={0} max={7999} precision={0} disabled={saving} style={{ width: "100%" }} />
          </Form.Item>
          {validation ? <Alert type="error" title={validation} showIcon style={{ marginBottom: 16 }} /> : null}
        </Form>
      ) : null}
      {saving ? <Typography.Paragraph type="secondary"><Spin size="small" /> Vectorizing documents. This can take a long time with a local model.</Typography.Paragraph> : null}
      {results && results.some((entry) => !Number.isInteger(entry?.chunkCount)) ? (
        <List
          size="small"
          style={{ maxHeight: 240, overflowY: "auto" }}
          dataSource={results.filter((entry) => !Number.isInteger(entry?.chunkCount))}
          renderItem={(entry) => (
            <List.Item>
              <Typography.Text type="danger">{entry?.errorDescription || "Vectorization failed"}</Typography.Text>
            </List.Item>
          )}
        />
      ) : null}
    </Modal>
  );
}
