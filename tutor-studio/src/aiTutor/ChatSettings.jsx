import Form from "react-bootstrap/Form";

export default function ChatSettings({ topK, showSources, onTopKChange, onShowSourcesChange }) {
  return (
    <details className="runtime-settings">
      <summary>Settings & details</summary>
      <div className="runtime-settings-grid">
        <div><small>Tutor knowledge</small><strong>Enabled by tutor configuration</strong></div>
        <Form.Group controlId="runtime-top-k">
          <Form.Label>Retrieval depth</Form.Label>
          <Form.Range min={1} max={10} value={topK} onChange={(event) => onTopKChange(Number(event.target.value))} />
          <Form.Text>Top {topK}</Form.Text>
        </Form.Group>
        <Form.Check
          type="switch"
          id="runtime-show-sources"
          label="Show sources"
          checked={showSources}
          onChange={(event) => onShowSourcesChange(event.target.checked)}
        />
      </div>
    </details>
  );
}
