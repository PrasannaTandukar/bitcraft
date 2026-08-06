import Alert from "react-bootstrap/Alert";
import Button from "react-bootstrap/Button";
import Card from "react-bootstrap/Card";
import { useNavigate } from "react-router-dom";

export default function TutorKnowledgePanel({ canManage }) {
  const navigate = useNavigate();
  return (
    <div>
      <div className="tutor-section-heading"><div><h2>Knowledge</h2><p>Documents retrieved by this tutor are captured into its activation snapshot.</p></div></div>
      <Card className="tutor-panel-card">
        <Card.Body>
          <Card.Title>Knowledge-source management</Card.Title>
          <Card.Text>Use the existing Files workspace to upload and vectorize documents. Tutor document attachment remains part of the existing knowledge-source workflow.</Card.Text>
          <Alert variant="info">After changing attached documents, activate a new revision to capture a reproducible effective document set for runtime use and benchmarking.</Alert>
          {canManage && <Button variant="outline-primary" onClick={() => navigate("/files")}>Open Files</Button>}
        </Card.Body>
      </Card>
    </div>
  );
}
