import { useState } from "react";
import Accordion from "react-bootstrap/Accordion";
import Alert from "react-bootstrap/Alert";
import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import Spinner from "react-bootstrap/Spinner";
import { useNavigate } from "react-router-dom";
import { getApiErrorDescription } from "../api/client.js";
import RevisionEditor from "./RevisionEditor.jsx";
import {
  activateTutorRevision,
  archiveTutorRevision,
  createTutorRevision,
  discardTutorRevision,
  publishTutorRevision,
} from "./tutorApi.js";
import { deploymentTarget, formatDateTime, revisionIsPublishable, revisionStatus, statusVariant } from "./tutorUtils.js";

const confirmationText = (action) => ({
  publish: "Publish this revision? Published revisions become immutable and can then be activated.",
  activate: "Activate this revision? Activating it makes this the tutor’s current runtime configuration. The previously active revision will be archived.",
  archive: "Archive this revision? An active tutor will stop using it until another revision is activated.",
  discard: "Discard this Draft revision permanently? This cannot be undone.",
}[action]);

export default function TutorRevisionsPanel({ tutorId, revisions, prompts, interactionModels, embeddingModels, canManage, onChanged, onCreatePrompt }) {
  const navigate = useNavigate();
  const [editing, setEditing] = useState(null);
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");

  const createRevision = async () => {
    if (busy) return;
    setBusy("create");
    setError("");
    try {
      const created = await createTutorRevision(tutorId);
      setEditing(created);
      onChanged();
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not create a Draft revision."));
    } finally {
      setBusy("");
    }
  };

  const perform = async (revision, action, request) => {
    if (busy || !window.confirm(confirmationText(action))) return;
    setBusy(`${revision.id}:${action}`);
    setError("");
    try {
      await request(tutorId, revision.id);
      if (editing?.id === revision.id) setEditing(null);
      onChanged();
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, `Could not ${action} the revision.`));
    } finally {
      setBusy("");
    }
  };

  return (
    <div>
      <div className="tutor-section-heading">
        <div><h2>Revisions</h2><p>Versioned, reproducible runtime configurations for this tutor.</p></div>
        {canManage && <Button onClick={createRevision} disabled={Boolean(busy)}>{busy === "create" && <Spinner size="sm" className="me-2" />}Create revision</Button>}
      </div>

      {error && <Alert variant="danger">{error}</Alert>}
      {revisions.length === 0 ? (
        <div className="tutor-empty-state">
          <h3>No revisions</h3>
          <p>Create a Draft, connect a prompt and interaction model, then explicitly publish and activate it.</p>
          {canManage && <Button variant="outline-primary" onClick={createRevision} disabled={Boolean(busy)}>Create first revision</Button>}
        </div>
      ) : (
        <Accordion alwaysOpen>
          {revisions.map((revision) => {
            const status = revisionStatus(revision.status);
            const isBusy = busy.startsWith(revision.id);
            const missing = [
              !revision.promptVersionId && "prompt version",
              !revision.interactionModelId && "interaction model",
              revision.interactionModelEnabled === false && "enabled interaction model",
              revision.runtimeEngineEnabled === false && "enabled runtime engine",
            ].filter(Boolean);
            return (
              <Accordion.Item eventKey={revision.id} key={revision.id}>
                <Accordion.Header>
                  <span className="revision-accordion-title">Revision {revision.versionNumber}</span>
                  <Badge bg={statusVariant(status)} className="ms-2">{status}</Badge>
                  {revision.isActive && <span className="revision-active-text">Current runtime configuration</span>}
                </Accordion.Header>
                <Accordion.Body>
                  <div className="revision-summary-grid">
                    <div><span>Prompt version</span><strong>{revision.promptVersionNumber ? `Prompt v${revision.promptVersionNumber} · ${revision.promptVersionName}` : "Not selected"}</strong></div>
                    <div><span>Interaction model</span><strong>{revision.interactionModelName ? `${revision.interactionModelName} · ${revision.providerModelName}` : "Not selected"}</strong></div>
                    <div><span>Query embedding</span><strong>{revision.queryEmbeddingModelName ? `${revision.queryEmbeddingModelName} · ${revision.queryEmbeddingDimensions} dimensions` : "None"}</strong></div>
                    <div><span>Deployment</span><strong>{revision.deploymentTargetDisplay || deploymentTarget(revision.deploymentTarget)}</strong></div>
                    <div><span>Created</span><strong>{formatDateTime(revision.createdUtc)}</strong></div>
                    <div><span>Published</span><strong>{formatDateTime(revision.publishedUtc)}</strong></div>
                    <div><span>Activated</span><strong>{formatDateTime(revision.activatedUtc)}</strong></div>
                    <div><span>Archived</span><strong>{formatDateTime(revision.archivedUtc)}</strong></div>
                    <div><span>Activation slug</span><strong>{revision.activationSlug || "—"}</strong></div>
                    <div><span>Snapshot</span><strong>{revision.latestSnapshotId ? "Captured and ready" : "Not captured"}</strong></div>
                  </div>

                  {status === "Draft" && missing.length > 0 && <Alert variant="warning" className="mt-3 mb-0">Before publishing, select: {missing.join(" and ")}.</Alert>}

                  <div className="revision-actions mt-3">
                    {status === "Draft" && canManage && <>
                      <Button size="sm" onClick={() => setEditing(revision)}>Configure</Button>
                      <Button size="sm" variant="outline-success" disabled={!revisionIsPublishable(revision) || isBusy} onClick={() => perform(revision, "publish", publishTutorRevision)}>Publish</Button>
                      <Button size="sm" variant="outline-danger" disabled={isBusy} onClick={() => perform(revision, "discard", discardTutorRevision)}>Discard</Button>
                    </>}
                    {status === "Published" && canManage && <>
                      <Button size="sm" variant="success" disabled={isBusy} onClick={() => perform(revision, "activate", activateTutorRevision)}>Activate</Button>
                      <Button size="sm" variant="outline-secondary" disabled={isBusy} onClick={() => perform(revision, "archive", archiveTutorRevision)}>Archive</Button>
                    </>}
                    {status === "Active" && canManage && <>
                      <Button size="sm" variant="outline-primary" onClick={() => setEditing(revision)}>View</Button>
                      <Button size="sm" variant="outline-secondary" disabled={isBusy} onClick={() => perform(revision, "archive", archiveTutorRevision)}>Archive</Button>
                      <Button size="sm" onClick={() => navigate(`/benchmarking?tab=new&tutor=${tutorId}&revision=${revision.id}`)}>Benchmark this revision</Button>
                    </>}
                    {status === "Archived" && <Button size="sm" variant="outline-primary" onClick={() => setEditing(revision)}>View</Button>}
                    {!canManage && status !== "Archived" && <Button size="sm" variant="outline-primary" onClick={() => setEditing(revision)}>View configuration</Button>}
                    {isBusy && <span className="text-muted small"><Spinner size="sm" className="me-2" />Updating revision…</span>}
                  </div>
                </Accordion.Body>
              </Accordion.Item>
            );
          })}
        </Accordion>
      )}

      {editing && (
        <RevisionEditor
          key={editing.id}
          tutorId={tutorId}
          revision={editing}
          prompts={prompts}
          interactionModels={interactionModels}
          embeddingModels={embeddingModels}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); onChanged(); }}
          onCreatePrompt={() => { setEditing(null); onCreatePrompt(); }}
        />
      )}
    </div>
  );
}
