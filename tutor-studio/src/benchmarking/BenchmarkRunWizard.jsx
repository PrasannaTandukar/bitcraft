import { useEffect, useMemo, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import Card from "react-bootstrap/Card";
import Col from "react-bootstrap/Col";
import Form from "react-bootstrap/Form";
import ListGroup from "react-bootstrap/ListGroup";
import ProgressBar from "react-bootstrap/ProgressBar";
import Row from "react-bootstrap/Row";
import Spinner from "react-bootstrap/Spinner";
import { getApiErrorDescription } from "../api/client.js";
import BenchmarkCaseEditor from "./BenchmarkCaseEditor.jsx";
import { createBenchmarkRun, listBenchmarkTemplates, listTutorRevisions } from "./benchmarkingApi.js";
import { emptyBenchmarkCase, formatPercent, isUsableRevision, METRIC_DETAILS, metricName, parseMetrics, revisionStatus } from "./benchmarkingUtils.js";

const STEPS = ["Tutor & revision", "Template", "Test cases", "Review & launch"];

export default function BenchmarkRunWizard({ tutors, tutorsLoading, tutorsError, initialTutorId, initialRevisionId, onSelectionChange, onConfigureTutor, onOpenTemplates, onCreated }) {
  const [step, setStep] = useState(1);
  const [tutorId, setTutorId] = useState(initialTutorId || "");
  const [revisions, setRevisions] = useState([]);
  const [revisionId, setRevisionId] = useState("");
  const [revisionsLoading, setRevisionsLoading] = useState(false);
  const [revisionsError, setRevisionsError] = useState("");
  const [templates, setTemplates] = useState([]);
  const [templateId, setTemplateId] = useState("");
  const [templatesLoading, setTemplatesLoading] = useState(true);
  const [templatesError, setTemplatesError] = useState("");
  const [automatic, setAutomatic] = useState(true);
  const [cases, setCases] = useState([emptyBenchmarkCase()]);
  const [useTutorKnowledge, setUseTutorKnowledge] = useState(true);
  const [topK, setTopK] = useState(5);
  const [consistencyRuns, setConsistencyRuns] = useState(1);
  const [validationError, setValidationError] = useState("");
  const [launchError, setLaunchError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const selectedTutor = tutors.find((tutor) => tutor.id === tutorId);
  const selectedRevision = revisions.find((revision) => revision.id === revisionId);
  const selectedTemplate = templates.find((template) => template.id === templateId);
  const selectedMetrics = useMemo(() => parseMetrics(selectedTemplate?.metricsJson), [selectedTemplate]);
  const totalOutputs = automatic ? cases.length * consistencyRuns : cases.length;

  useEffect(() => {
    const controller = new AbortController();
    listBenchmarkTemplates(controller.signal)
      .then((data) => {
        const enabled = (data ?? []).filter((template) => template.isEnabled);
        setTemplates(enabled);
        setTemplateId((current) => enabled.some((template) => template.id === current) ? current : "");
        setTemplatesError("");
      })
      .catch((error) => {
        if (error.name !== "AbortError") setTemplatesError(getApiErrorDescription(error, "Could not load benchmark templates."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setTemplatesLoading(false);
      });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (!tutorId) return undefined;

    const controller = new AbortController();
    listTutorRevisions(tutorId, controller.signal)
      .then((data) => {
        const loaded = data ?? [];
        setRevisions(loaded);
        const requested = loaded.find((revision) => revision.id === initialRevisionId);
        const active = loaded.find((revision) => revisionStatus(revision.status) === "Active");
        const nextRevision = requested || active;
        if (nextRevision) {
          setRevisionId(nextRevision.id);
          onSelectionChange(tutorId, nextRevision.id);
        }
      })
      .catch((error) => {
        if (error.name !== "AbortError") setRevisionsError(getApiErrorDescription(error, "Could not load tutor revisions."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setRevisionsLoading(false);
      });
    return () => controller.abort();
  }, [tutorId, initialRevisionId, onSelectionChange]);

  const selectTutor = (value) => {
    setTutorId(value);
    setRevisionId("");
    setRevisions([]);
    setRevisionsError("");
    setRevisionsLoading(Boolean(value));
    onSelectionChange(value, "");
  };

  const validateStep = (stepNumber) => {
    if (stepNumber === 1) {
      if (!selectedTutor) return "Select a tutor.";
      if (!selectedRevision) return "Select a tutor revision.";
      if (!isUsableRevision(selectedRevision)) {
        return "This revision is not benchmark-ready. Activate a configured revision so its immutable snapshot can be captured.";
      }
    }
    if (stepNumber === 2 && !selectedTemplate) return "Select an enabled benchmark template.";
    if (stepNumber === 3 || stepNumber === 4) {
      if (cases.length === 0) return "Add at least one test case.";
      if (cases.some((runCase) => !runCase.inputText.trim())) return "Every case requires a question.";
      if (!automatic && cases.some((runCase) => !runCase.actualOutput.trim())) return "Every existing-output case requires an actual answer.";
      if (cases.some((runCase) => {
        if (!runCase.metadataJson.trim()) return false;
        try { JSON.parse(runCase.metadataJson); return false; } catch { return true; }
      })) return "Case metadata must be valid JSON.";
      if (topK < 1 || topK > 10) return "Retrieval Top K must be between 1 and 10.";
      if (consistencyRuns < 1 || consistencyRuns > 10) return "Consistency runs must be between 1 and 10.";
      if (!automatic && consistencyRuns !== 1) return "Existing-answer evaluation requires consistency runs to be 1.";
      if (totalOutputs > 200) return "The expanded run cannot exceed 200 evaluated outputs.";
    }
    return "";
  };

  const goNext = () => {
    const error = validateStep(step);
    setValidationError(error);
    if (!error) setStep((current) => Math.min(4, current + 1));
  };

  const updateCase = (index, value) => setCases((current) => current.map((runCase, position) => position === index ? value : runCase));
  const removeCase = (index) => setCases((current) => current.filter((_, position) => position !== index));
  const duplicateCase = (index) => setCases((current) => [
    ...current.slice(0, index + 1),
    { ...current[index] },
    ...current.slice(index + 1),
  ]);
  const moveCase = (index, direction) => setCases((current) => {
    const target = index + direction;
    if (target < 0 || target >= current.length) return current;
    const next = [...current];
    [next[index], next[target]] = [next[target], next[index]];
    return next;
  });

  const setGenerationMode = (generate) => {
    setAutomatic(generate);
    if (!generate) setConsistencyRuns(1);
  };

  const launch = async () => {
    const errors = [1, 2, 3, 4].map(validateStep).filter(Boolean);
    if (errors.length || submitting) {
      setValidationError(errors[0] ?? "");
      return;
    }

    setSubmitting(true);
    setLaunchError("");
    try {
      const created = await createBenchmarkRun(tutorId, {
        benchmarkTemplateId: templateId,
        tutorRevisionId: revisionId,
        tutorConfigurationSnapshotId: null,
        interactionModelId: null,
        modelAlias: null,
        generateActualOutputs: automatic,
        useTutorKnowledge,
        topK,
        consistencyRuns,
        cases: cases.map((runCase) => ({
          inputText: runCase.inputText.trim(),
          actualOutput: automatic ? null : runCase.actualOutput.trim(),
          expectedOutput: runCase.expectedOutput.trim() || null,
          contextText: runCase.contextText.trim() || null,
          metadataJson: runCase.metadataJson.trim() || null,
        })),
      });
      onCreated(tutorId, created.id);
    } catch (error) {
      setLaunchError(getApiErrorDescription(error, "Could not launch the benchmark run."));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div>
      <div className="benchmark-step-labels" aria-label="Benchmark setup progress">
        {STEPS.map((label, index) => (
          <span key={label} className={step === index + 1 ? "active" : step > index + 1 ? "complete" : ""}>
            {index + 1}. {label}
          </span>
        ))}
      </div>
      <ProgressBar now={step * 25} visuallyHidden className="mb-4" />

      {step === 1 && (
        <Card className="benchmark-panel-card">
          <Card.Body>
            <Card.Title>Select tutor and revision</Card.Title>
            <Card.Text className="text-muted">Choose the exact tutor configuration to execute and preserve with this run.</Card.Text>
            {tutorsError && <Alert variant="danger">{tutorsError}</Alert>}
            <Form.Group className="mb-3" controlId="benchmark-tutor">
              <Form.Label>Tutor *</Form.Label>
              <Form.Select value={tutorId} onChange={(event) => selectTutor(event.target.value)} disabled={tutorsLoading}>
                <option value="">Select a tutor</option>
                {tutors.map((tutor) => <option key={tutor.id} value={tutor.id}>{tutor.name}</option>)}
              </Form.Select>
              {selectedTutor?.description && <Form.Text>{selectedTutor.description}</Form.Text>}
            </Form.Group>
            {revisionsLoading && <p><Spinner size="sm" className="me-2" />Loading revisions…</p>}
            {revisionsError && <Alert variant="danger">{revisionsError}</Alert>}
            <Form.Group controlId="benchmark-revision">
              <Form.Label>Revision *</Form.Label>
              <Form.Select value={revisionId} onChange={(event) => { setRevisionId(event.target.value); onSelectionChange(tutorId, event.target.value); }} disabled={!tutorId || revisionsLoading}>
                <option value="">Select a revision</option>
                {revisions.filter((revision) => revisionStatus(revision.status) !== "Archived").map((revision) => (
                  <option key={revision.id} value={revision.id} disabled={!isUsableRevision(revision)}>
                    Revision {revision.versionNumber} · {revisionStatus(revision.status)}{revisionStatus(revision.status) === "Active" ? " · Active" : ""}{!isUsableRevision(revision) ? " · Configuration required" : ""}
                  </option>
                ))}
              </Form.Select>
            </Form.Group>
            {!revisionsLoading && tutorId && !revisions.some(isUsableRevision) && (
              <Alert variant="warning" className="mt-3">
                <p className="mb-2">This tutor does not have a configured revision yet.</p>
                <Button size="sm" variant="outline-primary" onClick={() => onConfigureTutor(tutorId)}>Configure tutor</Button>
              </Alert>
            )}
            {selectedRevision && (
              <div className="benchmark-selection-summary mt-3">
                <div><span>Status</span><strong>{revisionStatus(selectedRevision.status)}</strong></div>
                <div><span>Interaction model</span><strong>{selectedRevision.interactionModelName || selectedRevision.providerModelName || "Configured model"}</strong></div>
                <div><span>Model alias</span><strong>{selectedRevision.providerModelName || "Resolved when launched"}</strong></div>
                <div><span>Activation slug</span><strong>{selectedRevision.activationSlug || "Not active"}</strong></div>
              </div>
            )}
            {selectedRevision && !isUsableRevision(selectedRevision) && (
              <Alert variant="warning" className="mt-3">This revision needs a valid prompt, an enabled interaction model, and an activation snapshot before it can be benchmarked. Configure and activate it in the Tutor workspace.</Alert>
            )}
          </Card.Body>
        </Card>
      )}

      {step === 2 && (
        <Card className="benchmark-panel-card">
          <Card.Body>
            <Card.Title>Select evaluation template</Card.Title>
            <Card.Text className="text-muted">Templates define which approximate local indicators are calculated and their thresholds.</Card.Text>
            {templatesLoading && <p><Spinner size="sm" className="me-2" />Loading templates…</p>}
            {templatesError && <Alert variant="danger">{templatesError}</Alert>}
            {!templatesLoading && !templatesError && templates.length === 0 && (
              <div className="benchmark-empty-state">
                <h3>No enabled templates</h3>
                <p>Create an enabled evaluation template before launching a run.</p>
                <Button onClick={onOpenTemplates}>Open Templates</Button>
              </div>
            )}
            <div className="benchmark-template-grid">
              {templates.map((template) => (
                <button key={template.id} type="button" className={`benchmark-template-option ${templateId === template.id ? "selected" : ""}`} onClick={() => setTemplateId(template.id)}>
                  <strong>{template.name}</strong>
                  <span>{template.description || "No description provided."}</span>
                  <div className="d-flex flex-wrap gap-2 mt-2">
                    {parseMetrics(template.metricsJson).map((metric) => (
                      <Badge bg="light" text="dark" key={metric.key}>{metricName(metric.key)} · {formatPercent(metric.threshold)}</Badge>
                    ))}
                  </div>
                </button>
              ))}
            </div>
            {selectedMetrics.length > 0 && (
              <ListGroup className="mt-3">
                {selectedMetrics.map((metric) => (
                  <ListGroup.Item key={metric.key}>
                    <strong>{metricName(metric.key)}</strong> — threshold {formatPercent(metric.threshold)}
                    <div className="small text-muted">{METRIC_DETAILS[metric.key]?.description}</div>
                  </ListGroup.Item>
                ))}
              </ListGroup>
            )}
          </Card.Body>
        </Card>
      )}

      {step === 3 && (
        <div>
          <Card className="benchmark-panel-card mb-3">
            <Card.Body>
              <Card.Title>Add test cases</Card.Title>
              <Form.Check type="radio" id="automatic-generation" name="generation-mode" label="Generate tutor answers automatically" checked={automatic} onChange={() => setGenerationMode(true)} />
              <Form.Check type="radio" id="existing-answers" name="generation-mode" label="Evaluate existing answers (Advanced)" checked={!automatic} onChange={() => setGenerationMode(false)} />
              <Alert variant="info" className="mt-3 mb-3">
                Expected answers enable correctness scoring. Context supports meaningful faithfulness and groundedness indicators. Tutor knowledge retrieves context automatically; supplied context overrides retrieval for that case.
              </Alert>
              <Row className="g-3">
                <Col md={4}>
                  <Form.Check id="use-tutor-knowledge" label="Use tutor knowledge" checked={useTutorKnowledge} onChange={(event) => setUseTutorKnowledge(event.target.checked)} />
                </Col>
                <Col md={4}>
                  <Form.Group controlId="retrieval-top-k">
                    <Form.Label>Retrieval Top K</Form.Label>
                    <Form.Control type="number" min="1" max="10" value={topK} onChange={(event) => setTopK(Number(event.target.value))} disabled={!useTutorKnowledge} />
                  </Form.Group>
                </Col>
                <Col md={4}>
                  <Form.Group controlId="consistency-runs">
                    <Form.Label>Consistency runs</Form.Label>
                    <Form.Control type="number" min="1" max="10" value={consistencyRuns} onChange={(event) => setConsistencyRuns(Number(event.target.value))} disabled={!automatic} />
                  </Form.Group>
                </Col>
              </Row>
              {automatic && consistencyRuns > 1 && <Alert variant="warning" className="mt-3 mb-0">Consistency runs multiply generated outputs: {cases.length} questions × {consistencyRuns} repeats = {totalOutputs} outputs.</Alert>}
            </Card.Body>
          </Card>
          <div className="d-grid gap-3">
            {cases.map((runCase, index) => (
              <BenchmarkCaseEditor key={index} runCase={runCase} index={index} total={cases.length} automatic={automatic} onChange={updateCase} onRemove={removeCase} onDuplicate={duplicateCase} onMove={moveCase} />
            ))}
          </div>
          <Button className="mt-3" variant="outline-primary" onClick={() => setCases((current) => [...current, emptyBenchmarkCase()])} disabled={cases.length >= 200}>Add case</Button>
        </div>
      )}

      {step === 4 && (
        <Card className="benchmark-panel-card">
          <Card.Body>
            <Card.Title>Review and launch</Card.Title>
            <div className="benchmark-review-grid">
              <div><span>Tutor</span><strong>{selectedTutor?.name}</strong></div>
              <div><span>Revision</span><strong>Revision {selectedRevision?.versionNumber} · {revisionStatus(selectedRevision?.status)}</strong></div>
              <div><span>Model</span><strong>{selectedRevision?.providerModelName || selectedRevision?.interactionModelName || "Configured model"}</strong></div>
              <div><span>Template</span><strong>{selectedTemplate?.name}</strong></div>
              <div><span>Metrics</span><strong>{selectedMetrics.map((metric) => metricName(metric.key)).join(", ") || "None"}</strong></div>
              <div><span>Logical questions</span><strong>{cases.length}</strong></div>
              <div><span>Consistency runs</span><strong>{consistencyRuns}</strong></div>
              <div><span>Total evaluated outputs</span><strong>{totalOutputs}</strong></div>
              <div><span>Tutor knowledge</span><strong>{useTutorKnowledge ? `Enabled · Top ${topK}` : "Disabled"}</strong></div>
            </div>
          </Card.Body>
        </Card>
      )}

      {validationError && <Alert variant="warning" className="mt-3">{validationError}</Alert>}
      {launchError && <Alert variant="danger" className="mt-3">{launchError}</Alert>}
      <div className="d-flex justify-content-between mt-4">
        <Button variant="outline-secondary" onClick={() => { setValidationError(""); setStep((current) => Math.max(1, current - 1)); }} disabled={step === 1 || submitting}>Back</Button>
        {step < 4 ? (
          <Button onClick={goNext}>Continue</Button>
        ) : (
          <Button onClick={launch} disabled={submitting || Boolean(validateStep(4))}>
            {submitting ? <><Spinner size="sm" className="me-2" />Launching…</> : "Launch benchmark"}
          </Button>
        )}
      </div>
    </div>
  );
}
