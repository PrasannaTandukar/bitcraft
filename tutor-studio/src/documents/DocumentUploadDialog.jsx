import { useEffect, useRef, useState } from "react";
import { Alert, Button, Form, Modal, Spinner } from "react-bootstrap";
import { getApiErrorDescription } from "../api/client.js";
import {
  acceptedDocumentExtensions,
  defaultMaximumUploadLabel,
  replaceDocumentContent,
  uploadDocument,
} from "./documentApi.js";

export default function DocumentUploadDialog({ document, folders, onClose, onSaved }) {
  const [file, setFile] = useState(null);
  const [folderId, setFolderId] = useState(document?.folderId ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const requestRef = useRef(null);

  useEffect(() => () => requestRef.current?.abort(), []);

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (!file || busy) {
      if (!file) setError("Select a supported file to continue.");
      return;
    }

    setBusy(true);
    setError("");
    const controller = new AbortController();
    requestRef.current = controller;
    let saved = null;
    try {
      saved = document
        ? await replaceDocumentContent(document.id, file, controller.signal)
        : await uploadDocument(file, folderId || null, controller.signal);
      if (!saved || typeof saved !== "object" || !saved.id) {
        throw new Error("Invalid document response.");
      }
    } catch (requestError) {
      if (requestError.name !== "AbortError") {
        setError(getApiErrorDescription(
          requestError,
          "The file could not be uploaded or its text could not be extracted.",
        ));
      }
    } finally {
      if (!controller.signal.aborted) {
        requestRef.current = null;
        setBusy(false);
      }
    }

    if (saved) onSaved(saved, Boolean(document));
  };

  return (
    <Modal show onHide={busy ? undefined : onClose} backdrop={busy ? "static" : true} keyboard={!busy} centered>
      <Form onSubmit={handleSubmit}>
        <Modal.Header closeButton={!busy}>
          <Modal.Title>{document ? "Add or replace file content" : "Add file"}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {document && (
            <Alert variant="warning">
              Successful replacement removes existing knowledge chunks because they no longer match the source text.
            </Alert>
          )}
          {error && <Alert variant="danger">{error}</Alert>}

          <Form.Group controlId="document-file" className="mb-3">
            <Form.Label>File</Form.Label>
            <Form.Control
              type="file"
              accept={acceptedDocumentExtensions}
              disabled={busy}
              required
              onChange={(event) => setFile(event.target.files?.[0] ?? null)}
            />
            <Form.Text>
              Supported: TXT, Markdown, CSV, JSON, PDF and DOCX. Maximum size: {defaultMaximumUploadLabel}. OCR is not supported.
            </Form.Text>
          </Form.Group>

          {!document && (
            <Form.Group controlId="document-folder">
              <Form.Label>Folder</Form.Label>
              <Form.Select value={folderId} disabled={busy} onChange={(event) => setFolderId(event.target.value)}>
                <option value="">Root (no folder)</option>
                {folders.map((folder) => (
                  <option key={folder.id} value={folder.id}>{folder.name}</option>
                ))}
              </Form.Select>
            </Form.Group>
          )}

          {busy && (
            <div className="document-busy-message" role="status">
              <Spinner animation="border" size="sm" />
              <span>Uploading and extracting text…</span>
            </div>
          )}
        </Modal.Body>
        <Modal.Footer>
          <Button variant="outline-secondary" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button type="submit" disabled={busy || !file}>
            {document ? "Replace content" : "Upload file"}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}
