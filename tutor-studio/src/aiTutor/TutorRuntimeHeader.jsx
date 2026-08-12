import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";

export default function TutorRuntimeHeader({ tutor, canManage, onConfigure, onBenchmark }) {
  if (!tutor) return null;
  return (
    <section className="runtime-header-card">
      <div className="runtime-header-copy">
        <div className="d-flex flex-wrap gap-2 align-items-center mb-2">
          <h2>{tutor.tutorName}</h2>
          <Badge bg="success">Active · Revision {tutor.revisionNumber}</Badge>
        </div>
        {tutor.description && <p>{tutor.description}</p>}
        <div className="runtime-config-meta">
          <span><small>Interaction model</small><strong>{tutor.model.name}</strong></span>
          <span><small>Query embedding model</small><strong>{tutor.embeddingModel?.name || "Not required"}</strong></span>
          <span><small>Tutor knowledge</small><strong>{tutor.knowledgeDocumentCount} document{tutor.knowledgeDocumentCount === 1 ? "" : "s"}</strong></span>
        </div>
      </div>
      {canManage && (
        <div className="runtime-header-actions">
          <Button size="sm" variant="outline-secondary" onClick={onConfigure}>Configure Tutor</Button>
          <Button size="sm" variant="outline-primary" onClick={onBenchmark}>Benchmark Tutor</Button>
        </div>
      )}
    </section>
  );
}
