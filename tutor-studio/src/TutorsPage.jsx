import { useEffect, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import Card from "react-bootstrap/Card";
import Container from "react-bootstrap/Container";
import Form from "react-bootstrap/Form";
import Modal from "react-bootstrap/Modal";
import Spinner from "react-bootstrap/Spinner";
import { useSelector } from "react-redux";
import { useNavigate } from "react-router-dom";
import { getApiErrorDescription } from "./api/client.js";
import { createTutor, listTutors } from "./tutors/tutorApi.js";
import { formatDateTime, revisionStatus, statusVariant, tutorStatus } from "./tutors/tutorUtils.js";
import "./tutors/TutorDetails.css";

function AddTutorModal({ show, onClose, onCreated }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const submit = async (event) => {
    event.preventDefault();
    if (!name.trim() || saving) {
      if (!name.trim()) setError("Tutor name is required.");
      return;
    }

    setSaving(true);
    setError("");
    try {
      const created = await createTutor({ name: name.trim(), description: description.trim() || null });
      onCreated(created);
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not create the tutor."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal show={show} onHide={saving ? undefined : onClose} centered>
      <Form onSubmit={submit}>
        <Modal.Header closeButton={!saving}><Modal.Title>Create tutor</Modal.Title></Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger">{error}</Alert>}
          <Form.Group className="mb-3" controlId="new-tutor-name">
            <Form.Label>Name *</Form.Label>
            <Form.Control value={name} onChange={(event) => setName(event.target.value)} autoFocus maxLength={200} />
          </Form.Group>
          <Form.Group controlId="new-tutor-description">
            <Form.Label>Description</Form.Label>
            <Form.Control as="textarea" rows={3} value={description} onChange={(event) => setDescription(event.target.value)} maxLength={2000} />
          </Form.Group>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="outline-secondary" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button type="submit" disabled={saving || !name.trim()}>
            {saving && <Spinner size="sm" className="me-2" />}Create tutor
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}

export default function TutorsPage() {
  const navigate = useNavigate();
  const { roles = [] } = useSelector((state) => state.auth);
  const canManage = roles.includes("Editor") || roles.includes("Admin");
  const [tutors, setTutors] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    listTutors(controller.signal)
      .then((result) => {
        setTutors(result ?? []);
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") {
          setError(getApiErrorDescription(requestError, "Could not load tutors."));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [reload]);

  const openTutor = (tutorId) => navigate(`/tutors/${tutorId}`);

  return (
    <main className="tutor-workspace-page">
      <Container className="tutor-workspace-container">
        <header className="tutor-page-header">
          <div>
            <span className="tutor-eyebrow">Tutor configuration</span>
            <h1>Tutors</h1>
            <p>Build versioned prompts, revisions, runtime model choices, and benchmark-ready configurations.</p>
          </div>
          <div className="d-flex gap-2">
            <Button variant="outline-secondary" onClick={() => { setLoading(true); setReload((value) => value + 1); }} disabled={loading}>Refresh</Button>
            {canManage && <Button onClick={() => setShowCreate(true)}>Create tutor</Button>}
          </div>
        </header>

        {error && <Alert variant="danger">{error}</Alert>}
        {loading && <div className="tutor-loading"><Spinner /><span>Loading tutors…</span></div>}
        {!loading && !error && tutors.length === 0 && (
          <Card className="tutor-empty-card"><Card.Body><h2>No tutors yet</h2><p>Create a tutor to begin configuring its prompt and runtime revision.</p>{canManage && <Button onClick={() => setShowCreate(true)}>Create first tutor</Button>}</Card.Body></Card>
        )}
        {!loading && tutors.length > 0 && (
          <div className="tutor-list" role="list">
            {tutors.map((tutor) => {
              const status = tutorStatus(tutor.status);
              const activeStatus = revisionStatus(tutor.activeRevisionStatus);
              return (
                <Card
                  key={tutor.id}
                  className="tutor-list-card"
                  role="listitem"
                  tabIndex={0}
                  onClick={() => openTutor(tutor.id)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" || event.key === " ") {
                      event.preventDefault();
                      openTutor(tutor.id);
                    }
                  }}
                >
                  <Card.Body>
                    <div className="tutor-list-main">
                      <div>
                        <div className="d-flex flex-wrap align-items-center gap-2 mb-1">
                          <h2>{tutor.name}</h2>
                          <Badge bg={statusVariant(status)}>{status}</Badge>
                        </div>
                        <p>{tutor.description || "No description provided."}</p>
                      </div>
                      <Button variant="outline-primary" onClick={(event) => { event.stopPropagation(); openTutor(tutor.id); }}>
                        {canManage ? "Configure" : "Open"}
                      </Button>
                    </div>
                    <div className="tutor-list-meta">
                      <span><strong>Active revision</strong>{tutor.activeRevisionVersionNumber ? `Revision ${tutor.activeRevisionVersionNumber} · ${activeStatus}` : "Not activated"}</span>
                      <span><strong>Active prompt</strong>{tutor.activePromptVersionName ? `v${tutor.activePromptVersionNumber} · ${tutor.activePromptVersionName}` : "Not selected"}</span>
                      <span><strong>Updated</strong>{formatDateTime(tutor.modifiedUtc || tutor.createdUtc)}</span>
                    </div>
                  </Card.Body>
                </Card>
              );
            })}
          </div>
        )}
      </Container>
      <AddTutorModal
        show={showCreate}
        onClose={() => setShowCreate(false)}
        onCreated={(created) => navigate(`/tutors/${created.id}`)}
      />
    </main>
  );
}
