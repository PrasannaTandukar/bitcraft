import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import Card from "react-bootstrap/Card";
import { useNavigate } from "react-router-dom";
import { formatDateTime, revisionStatus, statusVariant, tutorStatus } from "./tutorUtils.js";

export default function TutorOverviewPanel({ tutor, prompts, revisions, canManage, onOpenTab }) {
  const navigate = useNavigate();
  const activeRevision = revisions.find((revision) => revision.id === tutor.activeRevisionId) || revisions.find((revision) => revisionStatus(revision.status) === "Active");
  const configuredDraft = revisions.find((revision) => revisionStatus(revision.status) === "Draft" && revision.promptVersionId && revision.interactionModelId);
  const published = revisions.find((revision) => revisionStatus(revision.status) === "Published");
  const ready = Boolean(activeRevision?.latestSnapshotId && activeRevision.promptVersionId && activeRevision.interactionModelId);
  const checklist = [
    { label: "Tutor created", complete: true },
    { label: "Prompt version created", complete: prompts.length > 0, action: "Create first prompt", tab: "prompts" },
    { label: "Interaction model selected", complete: revisions.some((revision) => revision.interactionModelId), action: "Configure revision", tab: "revisions" },
    { label: "Draft revision configured", complete: Boolean(configuredDraft) || revisions.some((revision) => ["Published", "Active", "Archived"].includes(revisionStatus(revision.status))), action: revisions.length ? "Configure revision" : "Create revision", tab: "revisions" },
    { label: "Revision published", complete: Boolean(published || activeRevision || revisions.some((revision) => revisionStatus(revision.status) === "Archived")), action: "Publish revision", tab: "revisions" },
    { label: "Revision activated", complete: Boolean(activeRevision), action: "Activate revision", tab: "revisions" },
    { label: "Ready for benchmarking", complete: ready, action: ready ? "Open Benchmarking" : "Complete configuration", tab: ready ? null : "revisions" },
  ];
  const status = tutorStatus(tutor.status);

  return (
    <div>
      <div className="tutor-section-heading"><div><h2>Overview</h2><p>Runtime readiness and the current versioned configuration.</p></div></div>
      <div className="tutor-overview-grid">
        <Card className="tutor-panel-card">
          <Card.Body>
            <div className="d-flex justify-content-between align-items-start gap-3 mb-3"><Card.Title className="mb-0">Tutor</Card.Title><Badge bg={statusVariant(status)}>{status}</Badge></div>
            <dl className="tutor-definition-list">
              <div><dt>Name</dt><dd>{tutor.name}</dd></div>
              <div><dt>Description</dt><dd>{tutor.description || "No description provided."}</dd></div>
              <div><dt>Created</dt><dd>{formatDateTime(tutor.createdUtc)}</dd></div>
              <div><dt>Modified</dt><dd>{formatDateTime(tutor.modifiedUtc)}</dd></div>
            </dl>
          </Card.Body>
        </Card>
        <Card className="tutor-panel-card">
          <Card.Body>
            <Card.Title>Current runtime configuration</Card.Title>
            {activeRevision ? (
              <dl className="tutor-definition-list">
                <div><dt>Active revision</dt><dd>Revision {activeRevision.versionNumber}</dd></div>
                <div><dt>Prompt</dt><dd>{activeRevision.promptVersionName ? `Prompt v${activeRevision.promptVersionNumber} · ${activeRevision.promptVersionName}` : "—"}</dd></div>
                <div><dt>Interaction model</dt><dd>{activeRevision.interactionModelName || "—"}</dd></div>
                <div><dt>Provider model</dt><dd>{activeRevision.providerModelName || "—"}</dd></div>
                <div><dt>Activation slug</dt><dd>{activeRevision.activationSlug || "—"}</dd></div>
                <div><dt>Snapshot</dt><dd>{activeRevision.latestSnapshotId ? "Captured" : "Missing"}</dd></div>
              </dl>
            ) : <div className="tutor-inline-empty">No revision is active yet.</div>}
          </Card.Body>
        </Card>
      </div>

      <Card className="tutor-panel-card mt-4">
        <Card.Body>
          <Card.Title>Setup checklist</Card.Title>
          <div className="tutor-checklist">
            {checklist.map((item) => (
              <div key={item.label} className={item.complete ? "complete" : "incomplete"}>
                <span className="tutor-check-icon" aria-hidden="true">{item.complete ? "✓" : "○"}</span>
                <span className="tutor-check-label">{item.label}<small>{item.complete ? "Complete" : "Action required"}</small></span>
                {!item.complete && canManage && item.action && <Button size="sm" variant="outline-primary" onClick={() => item.tab && onOpenTab(item.tab)}>{item.action}</Button>}
                {item.label === "Ready for benchmarking" && ready && <Button size="sm" onClick={() => navigate(`/benchmarking?tab=new&tutor=${tutor.id}&revision=${activeRevision.id}`)}>Open Benchmarking</Button>}
              </div>
            ))}
          </div>
        </Card.Body>
      </Card>
    </div>
  );
}
