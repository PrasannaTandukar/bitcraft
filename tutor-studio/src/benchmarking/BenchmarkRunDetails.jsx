import { useEffect, useMemo, useState } from "react";
import Accordion from "react-bootstrap/Accordion";
import Alert from "react-bootstrap/Alert";
import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import Card from "react-bootstrap/Card";
import Col from "react-bootstrap/Col";
import ProgressBar from "react-bootstrap/ProgressBar";
import Row from "react-bootstrap/Row";
import Spinner from "react-bootstrap/Spinner";
import Table from "react-bootstrap/Table";
import { getApiErrorDescription } from "../api/client.js";
import { getBenchmarkProgress, getBenchmarkRun } from "./benchmarkingApi.js";
import {
  formatDateTime,
  formatNumber,
  formatPercent,
  formatProgressPercent,
  metricName,
  parseMetrics,
  runStatusLabel,
  safeParseJson,
  scorerDisplayName,
  statusVariant,
  summarizeMetrics,
} from "./benchmarkingUtils.js";

const terminalStatus = (status) => status === "Completed" || status === "Failed";

function SummaryCard({ label, value }) {
  return <Card className="benchmark-summary-card"><Card.Body><span>{label}</span><strong>{value}</strong></Card.Body></Card>;
}

export default function BenchmarkRunDetails({ tutorId, runId, initialRun, onBack }) {
  const initialStatus = initialRun?.status;
  const [summary, setSummary] = useState(initialRun ?? null);
  const [detail, setDetail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [pollingStopped, setPollingStopped] = useState(false);
  const [retryNonce, setRetryNonce] = useState(0);

  useEffect(() => {
    let disposed = false;
    let timer = null;
    let activeController = null;
    let consecutiveFailures = 0;

    const loadDetail = async () => {
      activeController = new AbortController();
      const loaded = await getBenchmarkRun(tutorId, runId, activeController.signal);
      if (!disposed) {
        setDetail(loaded);
        setSummary(loaded.summary);
        setLoading(false);
        setError("");
      }
    };

    const poll = async () => {
      activeController = new AbortController();
      try {
        const progress = await getBenchmarkProgress(tutorId, runId, activeController.signal);
        if (disposed) return;
        consecutiveFailures = 0;
        setSummary((current) => ({
          ...(current ?? {}),
          ...progress,
          progressPercent: Math.max(Number(current?.progressPercent) || 0, Number(progress.progressPercent) || 0),
        }));
        setError("");
        if (terminalStatus(progress.status)) {
          await loadDetail();
          return;
        }
        setLoading(false);
        timer = window.setTimeout(poll, 2000);
      } catch (requestError) {
        if (disposed || requestError.name === "AbortError") return;
        consecutiveFailures += 1;
        setLoading(false);
        setError(getApiErrorDescription(requestError, "Could not refresh benchmark progress."));
        if (consecutiveFailures >= 3) {
          setPollingStopped(true);
          return;
        }
        timer = window.setTimeout(poll, 2000);
      }
    };

    if (terminalStatus(initialStatus)) {
      loadDetail().catch((requestError) => {
        if (!disposed && requestError.name !== "AbortError") {
          setError(getApiErrorDescription(requestError, "Could not load benchmark results."));
          setLoading(false);
        }
      });
    } else {
      poll();
    }

    return () => {
      disposed = true;
      if (timer) window.clearTimeout(timer);
      activeController?.abort();
    };
  }, [tutorId, runId, retryNonce, initialStatus]);

  const cases = useMemo(() => detail?.cases ?? [], [detail]);
  const metricSummary = useMemo(() => summarizeMetrics(cases), [cases]);
  const passedCases = cases.filter((runCase) => runCase.passed === true).length;
  const failedCases = cases.filter((runCase) => runCase.passed === false).length;
  const status = runStatusLabel(summary);

  return (
    <div>
      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <Button variant="outline-secondary" onClick={onBack}>Back to run history</Button>
        <Badge bg={statusVariant(status)}>{status}</Badge>
      </div>

      <Card className="benchmark-panel-card mb-4">
        <Card.Body>
          <div className="d-flex flex-wrap justify-content-between gap-3">
            <div>
              <Card.Title>Benchmark run</Card.Title>
              <Card.Text className="text-muted mb-0">
                Revision {summary?.tutorRevisionNumber ?? "legacy"} · {summary?.modelAlias || "Model pending"} · {summary?.benchmarkTemplateName || "Benchmark template"}
              </Card.Text>
            </div>
            <div className="text-end small text-muted">Queued {formatDateTime(summary?.queuedUtc)}</div>
          </div>
          {!terminalStatus(summary?.status) && (
            <div className="mt-4" aria-live="polite">
              <div className="d-flex justify-content-between mb-1"><strong>{summary?.progressStage || "Queued"}</strong><span>{formatProgressPercent(summary?.progressPercent)}</span></div>
              <ProgressBar now={Math.max(0, Math.min(100, Number(summary?.progressPercent) || 0))} />
              <p className="mt-2 mb-0 text-muted">{summary?.progressMessage || "Waiting for the benchmark worker."} {summary?.completedCaseCount ?? 0} of {summary?.totalCaseCount ?? 0} outputs processed.</p>
            </div>
          )}
          {summary?.failureDescription && <Alert variant="danger" className="mt-3 mb-0"><strong>{summary.failureCode || "Benchmark failed"}</strong><div>{summary.failureDescription}</div></Alert>}
          {error && <Alert variant="warning" className="mt-3 mb-0">{error}</Alert>}
          {pollingStopped && <Button className="mt-3" onClick={() => { setError(""); setPollingStopped(false); setLoading(true); setRetryNonce((value) => value + 1); }}>Retry progress monitoring</Button>}
          {loading && <p className="mt-3 mb-0"><Spinner size="sm" className="me-2" />Loading benchmark run…</p>}
        </Card.Body>
      </Card>

      {detail && (
        <>
          <Row className="g-3 mb-4">
            <Col sm={6} xl={3}><SummaryCard label="Average score" value={formatPercent(summary?.averageScore)} /></Col>
            <Col sm={6} xl={3}><SummaryCard label="Pass rate" value={formatPercent(summary?.passRate)} /></Col>
            <Col sm={6} xl={3}><SummaryCard label="Passed cases" value={passedCases} /></Col>
            <Col sm={6} xl={3}><SummaryCard label="Failed cases" value={failedCases} /></Col>
            <Col sm={6} xl={3}><SummaryCard label="Evaluated outputs" value={summary?.totalCaseCount ?? cases.length} /></Col>
            <Col sm={6} xl={3}><SummaryCard label="Total latency" value={`${formatNumber(summary?.totalLatencyMs)} ms`} /></Col>
            <Col sm={6} xl={3}><SummaryCard label="Estimated tokens" value={formatNumber(summary?.totalTokens, 0)} /></Col>
            <Col sm={6} xl={3}><SummaryCard label="Tokens / second" value={formatNumber(summary?.tokensPerSecond)} /></Col>
            <Col sm={6} xl={3}><SummaryCard label="Estimated CO₂" value={`${formatNumber(summary?.estimatedCo2Grams, 6)} g`} /></Col>
            <Col sm={6} xl={3}><SummaryCard label="Engine" value={`${scorerDisplayName(summary?.engineName)}${summary?.engineVersion ? ` · ${summary.engineVersion}` : ""}`} /></Col>
          </Row>

          {summary?.engineName === "deepeval-deterministic" && (
            <Alert variant="info">This deterministic local scorer reports approximate token- and claim-overlap indicators. It is not an LLM judge or an academic-grade correctness or hallucination assessment.</Alert>
          )}

          <Card className="benchmark-panel-card mb-4">
            <Card.Body>
              <Card.Title>Metric summary</Card.Title>
              {metricSummary.length === 0 ? <p className="text-muted mb-0">No valid metric results were returned.</p> : (
                <Table responsive className="mb-0">
                  <thead><tr><th>Metric</th><th>Average</th><th>Threshold</th><th>Result</th></tr></thead>
                  <tbody>{metricSummary.map((metric) => (
                    <tr key={metric.key}>
                      <td>{metricName(metric.key)}</td>
                      <td>{formatPercent(metric.average)}</td>
                      <td>{formatPercent(metric.threshold)}</td>
                      <td><Badge bg={metric.passed === null ? "secondary" : metric.passed ? "success" : "danger"}>{metric.passed === null ? "Not available" : metric.passed ? "Pass" : "Fail"}</Badge></td>
                    </tr>
                  ))}</tbody>
                </Table>
              )}
            </Card.Body>
          </Card>

          <Card className="benchmark-panel-card">
            <Card.Body>
              <Card.Title>Case results</Card.Title>
              <Accordion alwaysOpen>
                {cases.map((runCase) => {
                  const metadata = safeParseJson(runCase.metadataJson, {});
                  const performance = safeParseJson(runCase.performanceJson, {});
                  const metrics = parseMetrics(runCase.metricsJson);
                  const logicalIndex = Number.isInteger(metadata?.logicalCaseIndex) ? metadata.logicalCaseIndex + 1 : runCase.caseIndex + 1;
                  const repeat = Number.isInteger(metadata?.consistencyRunIndex) ? metadata.consistencyRunIndex + 1 : null;
                  const caseResultLabel = runCase.passed === null || runCase.passed === undefined ? "Not evaluated" : runCase.passed ? "Pass" : "Fail";
                  const caseResultVariant = runCase.passed === null || runCase.passed === undefined ? "secondary" : runCase.passed ? "success" : "danger";
                  return (
                    <Accordion.Item eventKey={String(runCase.id)} key={runCase.id}>
                      <Accordion.Header>
                        <span className="benchmark-case-heading"><Badge bg={caseResultVariant}>{caseResultLabel}</Badge><strong>Case {logicalIndex}{repeat ? ` · Repeat ${repeat}` : ""}</strong><span>{formatPercent(runCase.score)}</span><span>{runCase.inputText}</span></span>
                      </Accordion.Header>
                      <Accordion.Body>
                        <dl className="benchmark-case-details">
                          <dt>Actual tutor answer</dt><dd>{runCase.actualOutput || "—"}</dd>
                          <dt>Expected answer</dt><dd>{runCase.expectedOutput || "Not supplied"}</dd>
                          <dt>Context used</dt><dd className="benchmark-prewrap">{runCase.contextText || "No context used"}</dd>
                          <dt>Evaluation reason</dt><dd>{runCase.reason || "No reason returned"}</dd>
                          <dt>Performance</dt><dd>{Number.isFinite(Number(performance?.latency_ms)) ? `${formatNumber(performance.latency_ms)} ms` : "Not reported"}{Number.isFinite(Number(performance?.estimated_tokens)) ? ` · ${formatNumber(performance.estimated_tokens, 0)} estimated tokens` : ""}</dd>
                          {runCase.metadataJson && <><dt>Metadata</dt><dd><code>{JSON.stringify(metadata)}</code></dd></>}
                        </dl>
                        {metrics.length > 0 && (
                          <Table responsive size="sm">
                            <thead><tr><th>Metric</th><th>Score</th><th>Threshold</th><th>Result</th><th>Reason</th></tr></thead>
                            <tbody>{metrics.map((metric) => <tr key={metric.key}><td>{metricName(metric.key)}</td><td>{formatPercent(metric.score)}</td><td>{formatPercent(metric.threshold)}</td><td>{metric.passed ? "Pass" : "Fail"}</td><td>{metric.reason || "—"}</td></tr>)}</tbody>
                          </Table>
                        )}
                        <details className="benchmark-advanced-details"><summary>Advanced details</summary><pre>{JSON.stringify({ metadata: safeParseJson(runCase.metadataJson), metrics: safeParseJson(runCase.metricsJson), performance, resources: safeParseJson(runCase.resourceJson) }, null, 2)}</pre></details>
                      </Accordion.Body>
                    </Accordion.Item>
                  );
                })}
              </Accordion>
              <details className="benchmark-advanced-details mt-3"><summary>Advanced run details</summary><pre>{JSON.stringify({ summary: safeParseJson(detail.summaryJson), systemMetrics: safeParseJson(detail.systemMetricsJson), rawResult: safeParseJson(detail.rawResultJson) }, null, 2)}</pre></details>
            </Card.Body>
          </Card>
        </>
      )}
    </div>
  );
}
