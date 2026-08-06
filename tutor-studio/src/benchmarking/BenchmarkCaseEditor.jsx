import Button from "react-bootstrap/Button";
import Card from "react-bootstrap/Card";
import Col from "react-bootstrap/Col";
import Form from "react-bootstrap/Form";
import Row from "react-bootstrap/Row";

export default function BenchmarkCaseEditor({ runCase, index, total, automatic, onChange, onRemove, onDuplicate, onMove }) {
  const set = (field, value) => onChange(index, { ...runCase, [field]: value });

  return (
    <Card className="benchmark-case-card">
      <Card.Header className="d-flex flex-wrap align-items-center justify-content-between gap-2">
        <strong>Case {index + 1}</strong>
        <div className="d-flex flex-wrap gap-2">
          <Button size="sm" variant="outline-secondary" onClick={() => onMove(index, -1)} disabled={index === 0}>Move up</Button>
          <Button size="sm" variant="outline-secondary" onClick={() => onMove(index, 1)} disabled={index === total - 1}>Move down</Button>
          <Button size="sm" variant="outline-primary" onClick={() => onDuplicate(index)}>Duplicate</Button>
          <Button size="sm" variant="outline-danger" onClick={() => onRemove(index)} disabled={total === 1}>Remove</Button>
        </div>
      </Card.Header>
      <Card.Body>
        <Form.Group className="mb-3" controlId={`benchmark-question-${index}`}>
          <Form.Label>Question *</Form.Label>
          <Form.Control as="textarea" rows={2} value={runCase.inputText} onChange={(event) => set("inputText", event.target.value)} />
        </Form.Group>
        {!automatic && (
          <Form.Group className="mb-3" controlId={`benchmark-actual-${index}`}>
            <Form.Label>Actual answer *</Form.Label>
            <Form.Control as="textarea" rows={3} value={runCase.actualOutput} onChange={(event) => set("actualOutput", event.target.value)} />
          </Form.Group>
        )}
        <Row className="g-3">
          <Col lg={6}>
            <Form.Group controlId={`benchmark-expected-${index}`}>
              <Form.Label>Expected answer</Form.Label>
              <Form.Control as="textarea" rows={3} value={runCase.expectedOutput} onChange={(event) => set("expectedOutput", event.target.value)} />
            </Form.Group>
          </Col>
          <Col lg={6}>
            <Form.Group controlId={`benchmark-context-${index}`}>
              <Form.Label>Context override</Form.Label>
              <Form.Control as="textarea" rows={3} value={runCase.contextText} onChange={(event) => set("contextText", event.target.value)} />
            </Form.Group>
          </Col>
          <Col xs={12}>
            <Form.Group controlId={`benchmark-metadata-${index}`}>
              <Form.Label>Metadata (JSON)</Form.Label>
              <Form.Control as="textarea" rows={2} value={runCase.metadataJson} onChange={(event) => set("metadataJson", event.target.value)} placeholder='{"difficulty":"easy"}' />
            </Form.Group>
          </Col>
        </Row>
      </Card.Body>
    </Card>
  );
}
