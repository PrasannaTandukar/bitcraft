import { useState } from "react";
import Accordion from "react-bootstrap/Accordion";
import Alert from "react-bootstrap/Alert";
import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import Card from "react-bootstrap/Card";
import Form from "react-bootstrap/Form";
import Spinner from "react-bootstrap/Spinner";
import { getApiErrorDescription } from "../api/client.js";
import { createPromptVersion } from "./tutorApi.js";
import { formatDateTime } from "./tutorUtils.js";

export default function PromptVersionsPanel({ tutorId, prompts, activePromptVersionId, canManage, onChanged }) {
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState("");
  const [content, setContent] = useState("");
  const [notes, setNotes] = useState("");
  const [setAsActive, setSetAsActive] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const reset = () => {
    setName("");
    setContent("");
    setNotes("");
    setSetAsActive(false);
    setError("");
    setShowForm(false);
  };

  const submit = async (event) => {
    event.preventDefault();
    if (!name.trim() || !content.trim() || saving) return;
    setSaving(true);
    setError("");
    try {
      await createPromptVersion(tutorId, {
        name: name.trim(),
        content,
        notes: notes.trim() || null,
        setAsActive,
      });
      reset();
      onChanged();
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not create the prompt version."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <div className="tutor-section-heading">
        <div><h2>Prompt Versions</h2><p>Immutable tutor instruction history. Create a new version whenever the instructions change.</p></div>
        {canManage && <Button onClick={() => setShowForm((value) => !value)}>{showForm ? "Close form" : "Create prompt version"}</Button>}
      </div>

      <Alert variant="info">Prompt versions cannot be edited after creation. Create a new version to make changes. The legacy active prompt is separate from the active Tutor Revision.</Alert>

      {showForm && canManage && (
        <Card className="tutor-panel-card mb-4">
          <Card.Body>
            <Card.Title>Create prompt version</Card.Title>
            {error && <Alert variant="danger">{error}</Alert>}
            <Form onSubmit={submit}>
              <Form.Group className="mb-3" controlId="prompt-version-name">
                <Form.Label>Name *</Form.Label>
                <Form.Control value={name} onChange={(event) => setName(event.target.value)} placeholder="Robotics Tutor Instructions" maxLength={200} />
              </Form.Group>
              <Form.Group className="mb-3" controlId="prompt-version-content">
                <Form.Label>Prompt content *</Form.Label>
                <Form.Control as="textarea" rows={10} value={content} onChange={(event) => setContent(event.target.value)} placeholder="You are a helpful robotics tutor…" />
              </Form.Group>
              <Form.Group className="mb-3" controlId="prompt-version-notes">
                <Form.Label>Notes</Form.Label>
                <Form.Control as="textarea" rows={2} value={notes} onChange={(event) => setNotes(event.target.value)} placeholder="What changed in this version?" />
              </Form.Group>
              <Form.Check
                className="mb-3"
                id="prompt-set-legacy-active"
                checked={setAsActive}
                onChange={(event) => setSetAsActive(event.target.checked)}
                label="Set as legacy active prompt"
              />
              <div className="d-flex justify-content-end gap-2">
                <Button variant="outline-secondary" onClick={reset} disabled={saving}>Cancel</Button>
                <Button type="submit" disabled={saving || !name.trim() || !content.trim()}>
                  {saving && <Spinner size="sm" className="me-2" />}Create immutable version
                </Button>
              </div>
            </Form>
          </Card.Body>
        </Card>
      )}

      {prompts.length === 0 ? (
        <div className="tutor-empty-state"><h3>No prompt versions</h3><p>A prompt defines how this tutor should behave and teach.</p>{canManage && <Button variant="outline-primary" onClick={() => setShowForm(true)}>Create first prompt</Button>}</div>
      ) : (
        <Accordion alwaysOpen>
          {prompts.map((prompt) => (
            <Accordion.Item eventKey={prompt.id} key={prompt.id}>
              <Accordion.Header>
                <span className="prompt-accordion-title">Prompt v{prompt.versionNumber} · {prompt.name}</span>
                {activePromptVersionId === prompt.id && <Badge bg="secondary" className="ms-2">Legacy active</Badge>}
              </Accordion.Header>
              <Accordion.Body>
                <div className="tutor-detail-meta mb-3">
                  <span><strong>Created</strong>{formatDateTime(prompt.createdUtc)}</span>
                  <span><strong>Notes</strong>{prompt.notes || "—"}</span>
                </div>
                <Form.Label>Prompt content</Form.Label>
                <pre className="prompt-content-view">{prompt.content}</pre>
              </Accordion.Body>
            </Accordion.Item>
          ))}
        </Accordion>
      )}
    </div>
  );
}
