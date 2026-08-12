import { useEffect, useMemo, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import Card from "react-bootstrap/Card";
import Form from "react-bootstrap/Form";
import Spinner from "react-bootstrap/Spinner";
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
  const [selectedDocumentId, setSelectedDocumentId] = useState("");
  const [selectedFolderId, setSelectedFolderId] = useState("");
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
              try {
                return [document.id, await getVectorizationStatus(document.id, controller.signal)];
              } catch {
                return [document.id, null];
              }
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
  }, [tutorId, canManage, reload]);

  const availableDocuments = useMemo(() => documents.filter((document) =>
    attachedDocuments.every((attached) => attached.documentId !== document.id)), [documents, attachedDocuments]);
  const availableFolders = useMemo(() => folders.filter((folder) =>
    attachedFolders.every((attached) => attached.folderId !== folder.id)), [folders, attachedFolders]);
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
      setSelectedDocumentId("");
      setSelectedFolderId("");
      setLoading(true);
      setReload((value) => value + 1);
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not update tutor knowledge."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <div className="tutor-section-heading">
        <div><h2>Knowledge</h2><p>Attach vectorized documents now; activation freezes the exact effective set into the next snapshot.</p></div>
        <Button variant="outline-secondary" onClick={() => navigate("/files")}>Manage files</Button>
      </div>
      {error && <Alert variant="danger">{error}</Alert>}
      {loading && <div className="tutor-loading"><Spinner /><span>Loading tutor knowledge…</span></div>}
      {!loading && <>
        <Alert variant="info">
          The next activation will capture {effectiveDocumentIds.size} effective document{effectiveDocumentIds.size === 1 ? "" : "s"}. Changes here do not mutate the currently active snapshot.
        </Alert>

        {canManage && (
          <Card className="tutor-panel-card mb-3">
            <Card.Body>
              <Card.Title>Attach a document</Card.Title>
              <div className="d-flex gap-2 align-items-start">
                <Form.Select value={selectedDocumentId} onChange={(event) => setSelectedDocumentId(event.target.value)} disabled={saving}>
                  <option value="">Select a source document</option>
                  {availableDocuments.map((document) => (
                    <option key={document.id} value={document.id}>
                      {document.name}{document.vectorization?.isVectorized && !document.vectorization?.isStale ? " · Vectorized" : " · Not ready"}
                    </option>
                  ))}
                </Form.Select>
                <Button
                  disabled={!selectedDocumentId || saving || !selectedDocument?.vectorization?.isVectorized || selectedDocument?.vectorization?.isStale}
                  onClick={() => runMutation(() => attachTutorDocument(tutorId, selectedDocumentId))}
                >Attach</Button>
              </div>
              {selectedDocument && (!selectedDocument.vectorization?.isVectorized || selectedDocument.vectorization?.isStale) && (
                <Alert variant="warning" className="mt-3 mb-0">Vectorize this document in Files before attaching it as retrieval-ready tutor knowledge.</Alert>
              )}
            </Card.Body>
          </Card>
        )}

        <Card className="tutor-panel-card mb-3">
          <Card.Body>
            <Card.Title>Attached documents</Card.Title>
            {attachedDocuments.length === 0 ? <p className="text-muted mb-0">No documents are attached directly.</p> : (
              <div className="d-grid gap-2">
                {attachedDocuments.map((document) => (
                  <div className="d-flex justify-content-between align-items-center border rounded p-2" key={document.documentId}>
                    <span><strong>{document.documentName}</strong><br /><small className="text-muted">{document.embeddingModelName || "No embedding model"}{document.legacyCompatibility ? " · Legacy compatibility link" : ""}</small></span>
                    <div className="d-flex gap-2 align-items-center">
                      <Badge bg={document.isVectorized ? "success" : "warning"}>{document.isVectorized ? "Vectorized" : "Not ready"}</Badge>
                      {canManage && <Button size="sm" variant="outline-danger" disabled={saving} onClick={() => runMutation(() => detachTutorDocument(tutorId, document.documentId))}>Detach</Button>}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Card.Body>
        </Card>

        <Card className="tutor-panel-card">
          <Card.Body>
            <Card.Title>Attached folders</Card.Title>
            {canManage && <div className="d-flex gap-2 mb-3">
              <Form.Select value={selectedFolderId} onChange={(event) => setSelectedFolderId(event.target.value)} disabled={saving}>
                <option value="">Select a folder</option>
                {availableFolders.map((folder) => <option key={folder.id} value={folder.id}>{folder.name}</option>)}
              </Form.Select>
              <Button disabled={!selectedFolderId || saving} onClick={() => runMutation(() => attachTutorDocumentFolder(tutorId, selectedFolderId))}>Attach folder</Button>
            </div>}
            {attachedFolders.length === 0 ? <p className="text-muted mb-0">No folders are attached.</p> : attachedFolders.map((folder) => (
              <div className="d-flex justify-content-between align-items-center border rounded p-2 mb-2" key={folder.folderId}>
                <strong>{folder.folderName}</strong>
                {canManage && <Button size="sm" variant="outline-danger" disabled={saving} onClick={() => runMutation(() => detachTutorDocumentFolder(tutorId, folder.folderId))}>Detach</Button>}
              </div>
            ))}
          </Card.Body>
        </Card>
      </>}
    </div>
  );
}
