import { useEffect, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import Card from "react-bootstrap/Card";
import Form from "react-bootstrap/Form";
import Spinner from "react-bootstrap/Spinner";
import Table from "react-bootstrap/Table";
import { getApiErrorDescription } from "../api/client.js";
import { listBenchmarkRuns } from "./benchmarkingApi.js";
import BenchmarkRunDetails from "./BenchmarkRunDetails.jsx";
import { formatDateTime, formatPercent, runStatusLabel, scorerDisplayName, statusVariant } from "./benchmarkingUtils.js";

export default function BenchmarkRunHistory({ tutors, tutorsLoading, tutorsError, selectedTutorId, selectedRunId, onSelectTutor, onOpenRun, onCloseRun }) {
  const [runs, setRuns] = useState([]);
  const [loading, setLoading] = useState(Boolean(selectedTutorId));
  const [error, setError] = useState("");
  const [refreshNonce, setRefreshNonce] = useState(0);

  useEffect(() => {
    if (!selectedTutorId) return undefined;
    const controller = new AbortController();
    listBenchmarkRuns(selectedTutorId, controller.signal)
      .then((data) => {
        setRuns(data ?? []);
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load benchmark run history."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [selectedTutorId, refreshNonce]);

  const selectTutor = (value) => {
    setRuns([]);
    setError("");
    setLoading(Boolean(value));
    onSelectTutor(value);
  };

  const refresh = () => {
    setLoading(true);
    setRefreshNonce((value) => value + 1);
  };

  const closeRun = () => {
    refresh();
    onCloseRun();
  };

  const selectedRun = runs.find((run) => run.id === selectedRunId) ?? null;
  if (selectedTutorId && selectedRunId) {
    return <BenchmarkRunDetails tutorId={selectedTutorId} runId={selectedRunId} initialRun={selectedRun} onBack={closeRun} />;
  }

  return (
    <Card className="benchmark-panel-card">
      <Card.Body>
        <div className="d-flex flex-wrap justify-content-between gap-3 align-items-end mb-3">
          <Form.Group className="benchmark-tutor-filter" controlId="history-tutor">
            <Form.Label>Tutor</Form.Label>
            <Form.Select value={selectedTutorId} onChange={(event) => selectTutor(event.target.value)} disabled={tutorsLoading}>
              <option value="">Select a tutor to view its runs</option>
              {tutors.map((tutor) => <option key={tutor.id} value={tutor.id}>{tutor.name}</option>)}
            </Form.Select>
          </Form.Group>
          <Button variant="outline-secondary" onClick={refresh} disabled={!selectedTutorId || loading}>Refresh history</Button>
        </div>
        {tutorsError && <Alert variant="danger">{tutorsError}</Alert>}
        {error && <Alert variant="danger">{error}</Alert>}
        {loading && <p><Spinner size="sm" className="me-2" />Loading benchmark runs…</p>}
        {!selectedTutorId && !loading && <div className="benchmark-empty-state"><h3>Select a tutor</h3><p>Run history is scoped to tutor membership.</p></div>}
        {selectedTutorId && !loading && !error && runs.length === 0 && <div className="benchmark-empty-state"><h3>No benchmark runs</h3><p>This tutor has no previous runs.</p></div>}
        {runs.length > 0 && (
          <Table responsive hover className="align-middle benchmark-history-table">
            <thead><tr><th>Status</th><th>Revision</th><th>Model</th><th>Template</th><th>Queued</th><th>Completed</th><th>Cases</th><th>Average</th><th>Pass rate</th><th>Engine</th><th><span className="visually-hidden">Actions</span></th></tr></thead>
            <tbody>{runs.map((run) => {
              const status = runStatusLabel(run);
              return (
                <tr key={run.id} className="benchmark-clickable-row" onClick={() => onOpenRun(run.id)}>
                  <td><Badge bg={statusVariant(status)}>{status}</Badge>{run.failureDescription && <div className="small text-danger mt-1">{run.failureDescription}</div>}</td>
                  <td>{run.tutorRevisionNumber ? `Revision ${run.tutorRevisionNumber}` : run.tutorRevisionId ? "Revision recorded" : "Legacy"}</td>
                  <td>{run.modelAlias || "—"}</td>
                  <td>{run.benchmarkTemplateName || "Template"}</td>
                  <td>{formatDateTime(run.queuedUtc)}</td>
                  <td>{formatDateTime(run.completedUtc)}</td>
                  <td>{run.totalCaseCount}</td>
                  <td>{formatPercent(run.averageScore)}</td>
                  <td>{formatPercent(run.passRate)}</td>
                  <td>{scorerDisplayName(run.engineName)}</td>
                  <td><Button size="sm" variant="outline-primary" onClick={(event) => { event.stopPropagation(); onOpenRun(run.id); }}>View run</Button></td>
                </tr>
              );
            })}</tbody>
          </Table>
        )}
      </Card.Body>
    </Card>
  );
}
