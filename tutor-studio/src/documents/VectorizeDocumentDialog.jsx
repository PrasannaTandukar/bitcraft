import { useEffect, useMemo, useRef, useState } from "react";
import { Alert, Button, Form, Modal, Spinner } from "react-bootstrap";
import { getApiErrorDescription } from "../api/client.js";
import {
  getVectorizationStatus,
  listEmbeddingModels,
  vectorizeDocument,
} from "./documentApi.js";

function formatDate(value) {
  if (!value) return "Never";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "Unknown" : date.toLocaleString();
}

function modelLabel(model) {
  const dimensions = Number.isFinite(Number(model.embeddingDimensions))
    ? `${Number(model.embeddingDimensions).toLocaleString()} dimensions`
    : "Dimensions unavailable";
  return `${model.name} · ${model.providerModelName} · ${dimensions} · ${model.runtimeEngineName || "Runtime"}`;
}

export default function VectorizeDocumentDialog({ document, onClose, onVectorized }) {
  const [status, setStatus] = useState(null);
  const [models, setModels] = useState([]);
  const [modelId, setModelId] = useState("");
  const [chunkSize, setChunkSize] = useState("1000");
  const [overlap, setOverlap] = useState("100");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [successCount, setSuccessCount] = useState(null);
  const requestRef = useRef(null);

  useEffect(() => {
    const controller = new AbortController();
    requestRef.current = controller;
    Promise.all([
      getVectorizationStatus(document.id, controller.signal),
      listEmbeddingModels(controller.signal),
    ])
      .then(([statusValue, modelValues]) => {
        const usableModels = Array.isArray(modelValues)
          ? modelValues.filter((model) => model?.isEnabled && model?.isRuntimeEngineEnabled)
          : [];
        setStatus(statusValue && typeof statusValue === "object" ? statusValue : null);
        setModels(usableModels);
        const existingModelId = statusValue?.embeddingModelId;
        setModelId(usableModels.some((model) => model.id === existingModelId)
          ? existingModelId
          : (usableModels[0]?.id ?? ""));
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") {
          setError(getApiErrorDescription(requestError, "Vectorization settings could not be loaded."));
        }
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
    const size = Number(chunkSize);
    const overlapValue = Number(overlap);
    if (!Number.isInteger(size) || size < 100 || size > 8000) return "Chunk size must be a whole number from 100 to 8000.";
    if (!Number.isInteger(overlapValue) || overlapValue < 0 || overlapValue >= size) return "Chunk overlap must be a whole number from 0 up to one less than chunk size.";
    return "";
  }, [chunkSize, overlap]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (saving || validation || !modelId || !status?.hasRawText) return;
    setSaving(true);
    setError("");
    setSuccessCount(null);
    const controller = new AbortController();
    requestRef.current = controller;
    try {
      const result = await vectorizeDocument(document.id, {
        embeddingModelId: modelId,
        maxChunkLength: Number(chunkSize),
        chunkOverlap: Number(overlap),
      }, controller.signal);
      const refreshedStatus = await getVectorizationStatus(document.id, controller.signal);
      setStatus(refreshedStatus);
      setSuccessCount(Number(result?.chunkCount) || 0);
      onVectorized(document.id, refreshedStatus);
    } catch (requestError) {
      if (requestError.name !== "AbortError") {
        setError(getApiErrorDescription(
          requestError,
          "The embedding service could not process this document. Confirm the model is installed and the runtime is available.",
        ));
      }
    } finally {
      if (!controller.signal.aborted) {
        requestRef.current = null;
        setSaving(false);
      }
    }
  };

  return (
    <Modal show onHide={saving ? undefined : onClose} backdrop={saving ? "static" : true} keyboard={!saving} centered>
      <Form onSubmit={handleSubmit}>
        <Modal.Header closeButton={!saving}>
          <Modal.Title>{status?.isVectorized ? "Re-vectorize file" : "Vectorize file"}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          <p className="document-dialog-filename">{document.name}</p>
          {loading && (
            <div className="document-busy-message" role="status">
              <Spinner animation="border" size="sm" />
              <span>Loading vectorization settings…</span>
            </div>
          )}
          {error && <Alert variant="danger">{error}</Alert>}
          {successCount !== null && (
            <Alert variant="success">Vectorized successfully. Created {successCount} knowledge chunks.</Alert>
          )}

          {!loading && status && !status.hasRawText && (
            <Alert variant="warning">This document has no extracted text. Add file content before vectorizing it.</Alert>
          )}

          {!loading && models.length === 0 && (
            <Alert variant="warning">
              <strong>No enabled embedding model is available.</strong><br />
              Ask an administrator to configure an embedding model and ensure that the corresponding Ollama model is installed.
            </Alert>
          )}

          {!loading && models.length > 0 && (
            <>
              {status?.isVectorized && (
                <Alert variant="info">
                  This replaces {status.chunkCount} existing chunks after every new embedding has been generated successfully.
                </Alert>
              )}
              <Form.Group controlId="vectorize-model" className="mb-3">
                <Form.Label>Embedding model</Form.Label>
                <Form.Select value={modelId} disabled={saving} onChange={(event) => setModelId(event.target.value)} required>
                  {models.map((model) => <option key={model.id} value={model.id}>{modelLabel(model)}</option>)}
                </Form.Select>
              </Form.Group>
              <div className="document-vector-fields">
                <Form.Group controlId="vectorize-chunk-size">
                  <Form.Label>Chunk size</Form.Label>
                  <Form.Control type="number" min="100" max="8000" step="1" value={chunkSize} disabled={saving} onChange={(event) => setChunkSize(event.target.value)} />
                </Form.Group>
                <Form.Group controlId="vectorize-overlap">
                  <Form.Label>Chunk overlap</Form.Label>
                  <Form.Control type="number" min="0" max="7999" step="1" value={overlap} disabled={saving} onChange={(event) => setOverlap(event.target.value)} />
                </Form.Group>
              </div>
              {validation && <div className="text-danger mt-2" role="alert">{validation}</div>}
              <dl className="document-status-details mt-3">
                <div><dt>Existing chunks</dt><dd>{status?.chunkCount ?? 0}</dd></div>
                <div><dt>Last vectorized</dt><dd>{formatDate(status?.lastVectorizedUtc)}</dd></div>
              </dl>
            </>
          )}

          {saving && (
            <div className="document-busy-message" role="status">
              <Spinner animation="border" size="sm" />
              <span>Generating embeddings…</span>
            </div>
          )}
        </Modal.Body>
        <Modal.Footer>
          <Button variant="outline-secondary" onClick={onClose} disabled={saving}>Close</Button>
          <Button type="submit" disabled={saving || loading || Boolean(validation) || !modelId || !status?.hasRawText}>
            {status?.isVectorized ? "Re-vectorize" : "Vectorize"}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}
