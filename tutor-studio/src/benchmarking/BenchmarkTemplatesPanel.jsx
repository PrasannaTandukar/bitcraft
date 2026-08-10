import { useEffect, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import Card from "react-bootstrap/Card";
import Form from "react-bootstrap/Form";
import Modal from "react-bootstrap/Modal";
import Spinner from "react-bootstrap/Spinner";
import Table from "react-bootstrap/Table";
import { getApiErrorDescription } from "../api/client.js";
import { createBenchmarkTemplate, deleteBenchmarkTemplate, listBenchmarkTemplates } from "./benchmarkingApi.js";
import { formatPercent, METRIC_DETAILS, metricName, parseMetrics } from "./benchmarkingUtils.js";

const DEFAULT_METRICS = Object.keys(METRIC_DETAILS).map((key) => ({ key, threshold: 0.5, enabled: true }));
const EMPTY_FORM = { key: "", name: "", description: "", guidancePath: "", isEnabled: true };

export default function BenchmarkTemplatesPanel() {
  const [templates, setTemplates] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [refreshNonce, setRefreshNonce] = useState(0);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState(EMPTY_FORM);
  const [metrics, setMetrics] = useState(DEFAULT_METRICS);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    listBenchmarkTemplates(controller.signal)
      .then((data) => {
        setTemplates(data ?? []);
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load benchmark templates."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [refreshNonce]);

  const refresh = () => {
    setLoading(true);
    setRefreshNonce((value) => value + 1);
  };

  const closeCreate = () => {
    if (saving) return;
    setShowCreate(false);
    setForm(EMPTY_FORM);
    setMetrics(DEFAULT_METRICS);
    setFormError("");
  };

  const saveTemplate = async () => {
    if (!form.key.trim() || !form.name.trim()) {
      setFormError("Key and name are required.");
      return;
    }
    const enabledMetrics = metrics.filter((metric) => metric.enabled);
    if (enabledMetrics.length === 0) {
      setFormError("Enable at least one metric.");
      return;
    }
    setSaving(true);
    setFormError("");
    try {
      const created = await createBenchmarkTemplate({
        key: form.key.trim(),
        name: form.name.trim(),
        description: form.description.trim() || null,
        guidancePath: form.guidancePath.trim() || null,
        metricsJson: JSON.stringify(enabledMetrics),
        isEnabled: form.isEnabled,
      });
      setTemplates((current) => [...current, created].sort((left, right) => left.name.localeCompare(right.name)));
      setShowCreate(false);
      setForm(EMPTY_FORM);
      setMetrics(DEFAULT_METRICS);
    } catch (requestError) {
      setFormError(getApiErrorDescription(requestError, "Could not create the benchmark template."));
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteTarget || deleting) return;
    setDeleting(true);
    setDeleteError("");
    try {
      await deleteBenchmarkTemplate(deleteTarget.id);
      setTemplates((current) => current.filter((template) => template.id !== deleteTarget.id));
      setDeleteTarget(null);
    } catch (requestError) {
      setDeleteError(getApiErrorDescription(requestError, "This template could not be deleted. It may already be used by a benchmark run."));
    } finally {
      setDeleting(false);
    }
  };

  return (
    <>
      <Card className="benchmark-panel-card">
        <Card.Body>
          <div className="d-flex flex-wrap justify-content-between gap-2 align-items-center mb-3">
            <div><Card.Title>Benchmark templates</Card.Title><Card.Text className="text-muted mb-0">Reusable metric and threshold profiles for deterministic local evaluation.</Card.Text></div>
            <div className="d-flex gap-2"><Button variant="outline-secondary" onClick={refresh} disabled={loading}>Refresh</Button><Button onClick={() => setShowCreate(true)}>Create template</Button></div>
          </div>
          {error && <Alert variant="danger">{error}</Alert>}
          {loading && <p><Spinner size="sm" className="me-2" />Loading templates…</p>}
          {!loading && !error && templates.length === 0 && <div className="benchmark-empty-state"><h3>No benchmark templates</h3><p>Create a template to define evaluation metrics and thresholds.</p></div>}
          {templates.length > 0 && (
            <Table responsive className="align-middle">
              <thead><tr><th>Name</th><th>Status</th><th>Metrics and thresholds</th><th>Guidance metadata</th><th><span className="visually-hidden">Actions</span></th></tr></thead>
              <tbody>{templates.map((template) => (
                <tr key={template.id}>
                  <td><strong>{template.name}</strong><div className="small text-muted">{template.key}</div>{template.description && <div className="small">{template.description}</div>}</td>
                  <td><Badge bg={template.isEnabled ? "success" : "secondary"}>{template.isEnabled ? "Enabled" : "Disabled"}</Badge></td>
                  <td>{parseMetrics(template.metricsJson).length === 0 ? <span className="text-muted">Malformed or empty metrics payload</span> : <div className="d-flex flex-wrap gap-1">{parseMetrics(template.metricsJson).map((metric) => <Badge bg="light" text="dark" key={metric.key}>{metricName(metric.key)} · {formatPercent(metric.threshold)}</Badge>)}</div>}</td>
                  <td>{template.guidancePath ? <><span>{template.guidancePath}</span><div className="small text-muted">Metadata only; the current scorer does not load this file.</div></> : "—"}</td>
                  <td><Button size="sm" variant="outline-danger" onClick={() => { setDeleteError(""); setDeleteTarget(template); }}>Delete</Button></td>
                </tr>
              ))}</tbody>
            </Table>
          )}
        </Card.Body>
      </Card>

      <Modal show={showCreate} onHide={closeCreate} centered size="lg">
        <Modal.Header closeButton><Modal.Title>Create benchmark template</Modal.Title></Modal.Header>
        <Modal.Body>
          {formError && <Alert variant="danger">{formError}</Alert>}
          <Form.Group className="mb-3" controlId="template-key"><Form.Label>Key *</Form.Label><Form.Control value={form.key} onChange={(event) => setForm((current) => ({ ...current, key: event.target.value }))} placeholder="my-evaluation-profile" /></Form.Group>
          <Form.Group className="mb-3" controlId="template-name"><Form.Label>Name *</Form.Label><Form.Control value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} /></Form.Group>
          <Form.Group className="mb-3" controlId="template-description"><Form.Label>Description</Form.Label><Form.Control as="textarea" rows={2} value={form.description} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} /></Form.Group>
          <fieldset className="benchmark-metric-fieldset">
            <legend>Metrics and thresholds</legend>
            {metrics.map((metric) => (
              <div className="benchmark-metric-control" key={metric.key}>
                <Form.Check id={`template-metric-${metric.key}`} label={metricName(metric.key)} checked={metric.enabled} onChange={(event) => setMetrics((current) => current.map((item) => item.key === metric.key ? { ...item, enabled: event.target.checked } : item))} />
                <Form.Label htmlFor={`template-threshold-${metric.key}`}>Threshold</Form.Label>
                <Form.Control id={`template-threshold-${metric.key}`} type="number" min="0" max="1" step="0.05" value={metric.threshold} disabled={!metric.enabled} onChange={(event) => setMetrics((current) => current.map((item) => item.key === metric.key ? { ...item, threshold: Number(event.target.value) } : item))} />
                <small>{METRIC_DETAILS[metric.key].description}</small>
              </div>
            ))}
          </fieldset>
          <details className="benchmark-advanced-details mt-3">
            <summary>Advanced metadata</summary>
            <Form.Group className="mt-3" controlId="template-guidance-path"><Form.Label>Guidance path</Form.Label><Form.Control value={form.guidancePath} onChange={(event) => setForm((current) => ({ ...current, guidancePath: event.target.value }))} /><Form.Text>This is stored as metadata only. The deterministic scorer does not load or apply this path.</Form.Text></Form.Group>
          </details>
          <Form.Check className="mt-3" id="template-enabled" label="Enabled" checked={form.isEnabled} onChange={(event) => setForm((current) => ({ ...current, isEnabled: event.target.checked }))} />
        </Modal.Body>
        <Modal.Footer><Button variant="outline-secondary" onClick={closeCreate} disabled={saving}>Cancel</Button><Button onClick={saveTemplate} disabled={saving}>{saving ? <><Spinner size="sm" className="me-2" />Saving…</> : "Create template"}</Button></Modal.Footer>
      </Modal>

      <Modal show={Boolean(deleteTarget)} onHide={() => !deleting && setDeleteTarget(null)} centered>
        <Modal.Header closeButton={!deleting}><Modal.Title>Delete benchmark template</Modal.Title></Modal.Header>
        <Modal.Body>{deleteError && <Alert variant="danger">{deleteError}</Alert>}<p>Delete <strong>{deleteTarget?.name}</strong>? This cannot be undone.</p><p className="text-muted mb-0">Templates already used by benchmark runs cannot be deleted.</p></Modal.Body>
        <Modal.Footer><Button variant="outline-secondary" onClick={() => setDeleteTarget(null)} disabled={deleting}>Cancel</Button><Button variant="danger" onClick={confirmDelete} disabled={deleting}>{deleting ? <><Spinner size="sm" className="me-2" />Deleting…</> : "Delete template"}</Button></Modal.Footer>
      </Modal>
    </>
  );
}
