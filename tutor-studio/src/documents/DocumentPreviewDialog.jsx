import { useEffect, useState } from "react";
import { Alert, Button, Modal, Spinner } from "react-bootstrap";
import { getApiErrorDescription } from "../api/client.js";
import { getDocument } from "./documentApi.js";

function safeLength(value) {
  return typeof value === "string" ? value.length : 0;
}

export default function DocumentPreviewDialog({ document, canManage, onClose, onReplace }) {
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    getDocument(document.id, controller.signal)
      .then((value) => {
        if (value && typeof value === "object") setDetail(value);
        else setError("The document response was invalid.");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") {
          setError(getApiErrorDescription(requestError, "The extracted text could not be loaded."));
        }
      });
    return () => controller.abort();
  }, [document.id]);

  return (
    <Modal show onHide={onClose} size="lg" centered>
      <Modal.Header closeButton>
        <Modal.Title>Extracted text preview</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {!detail && !error && (
          <div className="document-busy-message" role="status">
            <Spinner animation="border" size="sm" />
            <span>Loading extracted text…</span>
          </div>
        )}
        {error && <Alert variant="danger">{error}</Alert>}
        {detail && (
          <>
            <dl className="document-preview-metadata">
              <div><dt>Filename</dt><dd>{detail.name || "Unnamed document"}</dd></div>
              <div><dt>Type</dt><dd>{detail.mimeType || "Unknown"}</dd></div>
              <div><dt>Characters</dt><dd>{safeLength(detail.rawText).toLocaleString()}</dd></div>
            </dl>
            {safeLength(detail.rawText) > 0 ? (
              <pre className="document-text-preview">{detail.rawText}</pre>
            ) : (
              <Alert variant="warning">No extracted text is stored for this document.</Alert>
            )}
          </>
        )}
      </Modal.Body>
      <Modal.Footer>
        {canManage && detail && (
          <Button variant="outline-primary" onClick={() => onReplace(document)}>
            {safeLength(detail.rawText) > 0 ? "Replace file content" : "Add file content"}
          </Button>
        )}
        <Button variant="secondary" onClick={onClose}>Close</Button>
      </Modal.Footer>
    </Modal>
  );
}
