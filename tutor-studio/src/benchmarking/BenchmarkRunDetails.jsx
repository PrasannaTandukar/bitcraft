import { ArrowLeftOutlined, ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Col, Collapse, Descriptions, Empty, Flex, Progress, Row, Space, Spin, Statistic, Table, Tag, Tooltip, Typography } from "antd";
import { useEffect, useMemo, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import StatusTag from "../components/StatusTag.jsx";
import { getBenchmarkProgress, getBenchmarkRun } from "./benchmarkingApi.js";
import {
  formatDateTime,
  formatNumber,
  formatPercent,
  metricName,
  metricTierBadge,
  parseMetrics,
  runStatusLabel,
  safeParseJson,
  scorerDisplayName,
  summarizeMetrics,
} from "./benchmarkingUtils.js";

const terminalStatus = (status) => status === "Completed" || status === "Failed";

function SummaryCard({ label, value }) {
  return <Card size="small"><Statistic title={label} value={value} /></Card>;
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
        setSummary((current) => ({ ...current, ...progress, progressPercent: Math.max(Number(current?.progressPercent) || 0, Number(progress.progressPercent) || 0) }));
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
    } else poll();

    return () => {
      disposed = true;
      if (timer) window.clearTimeout(timer);
      activeController?.abort();
    };
  }, [initialStatus, retryNonce, runId, tutorId]);

  const cases = useMemo(() => detail?.cases ?? [], [detail]);
  const metricSummary = useMemo(() => summarizeMetrics(cases), [cases]);
  const passedCases = cases.filter((runCase) => runCase.passed === true).length;
  const failedCases = cases.filter((runCase) => runCase.passed === false).length;
  const status = runStatusLabel(summary);
  const progressValue = Math.max(0, Math.min(100, Number(summary?.progressPercent) || 0));

  const metricColumns = [
    {
      title: "Metric",
      dataIndex: "key",
      render: (key) => {
        const badge = metricTierBadge(key);
        return (
          <Space orientation="horizontal" size={6}>
            <Tooltip title={badge.tooltip}>
              <Tag color={badge.color} style={{ cursor: "help" }}>{badge.symbol} {badge.label}</Tag>
            </Tooltip>
            <Typography.Text strong>{metricName(key)}</Typography.Text>
          </Space>
        );
      },
    },
    { title: "Average", dataIndex: "average", render: formatPercent },
    { title: "Threshold", dataIndex: "threshold", render: formatPercent },
    { title: "Result", key: "result", render: (_, metric) => <Tag color={metric.passed === null ? "default" : metric.passed ? "success" : "error"}>{metric.passed === null ? "Not available" : metric.passed ? "Pass" : "Fail"}</Tag> },
  ];

  const caseItems = cases.map((runCase) => {
    const metadata = safeParseJson(runCase.metadataJson, {});
    const performance = safeParseJson(runCase.performanceJson, {});
    const metrics = parseMetrics(runCase.metricsJson);
    const logicalIndex = Number.isInteger(metadata?.logicalCaseIndex) ? metadata.logicalCaseIndex + 1 : runCase.caseIndex + 1;
    const repeat = Number.isInteger(metadata?.consistencyRunIndex) ? metadata.consistencyRunIndex + 1 : null;
    const caseResultLabel = runCase.passed === null || runCase.passed === undefined ? "Not evaluated" : runCase.passed ? "Pass" : "Fail";
    return {
      key: String(runCase.id),
      label: (
        <Flex align="center" gap={8} wrap>
          <StatusTag value={caseResultLabel} />
          <Typography.Text strong>Case {logicalIndex}{repeat ? ` · Repeat ${repeat}` : ""}</Typography.Text>
          <Typography.Text type="secondary">{formatPercent(runCase.score)}</Typography.Text>
          <Typography.Text ellipsis={{ tooltip: runCase.inputText }} className="benchmark-case-label">{runCase.inputText}</Typography.Text>
        </Flex>
      ),
      children: (
        <Space orientation="vertical" size={16} style={{ width: "100%" }}>
          <Descriptions column={1} size="small">
            <Descriptions.Item label="Actual tutor answer"><Typography.Paragraph className="benchmark-prewrap">{runCase.actualOutput || "—"}</Typography.Paragraph></Descriptions.Item>
            <Descriptions.Item label="Expected answer">{runCase.expectedOutput || "Not supplied"}</Descriptions.Item>
            <Descriptions.Item label="Context used"><Typography.Paragraph className="benchmark-prewrap">{runCase.contextText || "No context used"}</Typography.Paragraph></Descriptions.Item>
            <Descriptions.Item label="Evaluation reason">{runCase.reason || "No reason returned"}</Descriptions.Item>
            <Descriptions.Item label="Performance">{Number.isFinite(Number(performance?.latency_ms)) ? `${formatNumber(performance.latency_ms)} ms` : "Not reported"}{Number.isFinite(Number(performance?.estimated_tokens)) ? ` · ${formatNumber(performance.estimated_tokens, 0)} estimated tokens` : ""}</Descriptions.Item>
            {runCase.metadataJson ? <Descriptions.Item label="Metadata"><Typography.Text code>{JSON.stringify(metadata)}</Typography.Text></Descriptions.Item> : null}
          </Descriptions>
          {metrics.length ? <Table size="small" rowKey="key" pagination={false} scroll={{ x: 700 }} columns={[
            {
              title: "Metric",
              dataIndex: "key",
              render: (key) => {
                const badge = metricTierBadge(key);
                return (
                  <Space orientation="horizontal" size={6}>
                    <Tooltip title={badge.tooltip}>
                      <Tag color={badge.color} style={{ cursor: "help" }}>{badge.symbol} {badge.label}</Tag>
                    </Tooltip>
                    <Typography.Text strong>{metricName(key)}</Typography.Text>
                  </Space>
                );
              },
            },
            { title: "Score", dataIndex: "score", render: formatPercent },
            { title: "Threshold", dataIndex: "threshold", render: formatPercent },
            { title: "Result", dataIndex: "passed", render: (value) => <Tag color={value ? "success" : "error"}>{value ? "Pass" : "Fail"}</Tag> },
            { title: "Reason", dataIndex: "reason", render: (value) => value || "—" },
          ]} dataSource={metrics} /> : null}
          <Collapse ghost items={[{ key: "advanced", label: "Advanced details", children: <pre className="workspace-code">{JSON.stringify({ metadata: safeParseJson(runCase.metadataJson), metrics: safeParseJson(runCase.metricsJson), performance, resources: safeParseJson(runCase.resourceJson) }, null, 2)}</pre> }]} />
        </Space>
      ),
    };
  });

  return (
    <div className="benchmark-panel-stack">
      <Flex justify="space-between" align="center" gap={12} wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={onBack}>Run history</Button>
        <StatusTag value={status} />
      </Flex>

      <Card title="Benchmark run" extra={<Typography.Text type="secondary">Queued {formatDateTime(summary?.queuedUtc)}</Typography.Text>}>
        <Typography.Paragraph type="secondary">Revision {summary?.tutorRevisionNumber ?? "legacy"} · {summary?.modelAlias || "Model pending"} · {summary?.benchmarkTemplateName || "Benchmark template"}</Typography.Paragraph>
        {!terminalStatus(summary?.status) ? (
          <div aria-live="polite">
            <Flex justify="space-between"><Typography.Text strong>{summary?.progressStage || "Queued"}</Typography.Text><Typography.Text>{progressValue.toFixed(0)}%</Typography.Text></Flex>
            <Progress percent={progressValue} status="active" showInfo={false} />
            <Typography.Text type="secondary">{summary?.progressMessage || "Waiting for the benchmark worker."} {summary?.completedCaseCount ?? 0} of {summary?.totalCaseCount ?? 0} outputs processed.</Typography.Text>
          </div>
        ) : null}
        {summary?.failureDescription ? <Alert type="error" title={summary.failureCode || "Benchmark failed"} description={summary.failureDescription} showIcon style={{ marginTop: 16 }} /> : null}
        {error ? <Alert type="warning" title={error} showIcon style={{ marginTop: 16 }} /> : null}
        {pollingStopped ? <Button icon={<ReloadOutlined />} style={{ marginTop: 16 }} onClick={() => { setError(""); setPollingStopped(false); setLoading(true); setRetryNonce((value) => value + 1); }}>Retry progress monitoring</Button> : null}
        {loading ? <Typography.Paragraph style={{ marginTop: 16 }}><Spin size="small" /> Loading benchmark run…</Typography.Paragraph> : null}
      </Card>

      {detail ? (
        <>
          <Row gutter={[12, 12]}>
            <Col xs={12} md={8} xl={6}><SummaryCard label="Average score" value={formatPercent(summary?.averageScore)} /></Col>
            <Col xs={12} md={8} xl={6}><SummaryCard label="Pass rate" value={formatPercent(summary?.passRate)} /></Col>
            <Col xs={12} md={8} xl={6}><SummaryCard label="Passed cases" value={passedCases} /></Col>
            <Col xs={12} md={8} xl={6}><SummaryCard label="Failed cases" value={failedCases} /></Col>
            <Col xs={12} md={8} xl={6}><SummaryCard label="Evaluated outputs" value={summary?.totalCaseCount ?? cases.length} /></Col>
            <Col xs={12} md={8} xl={6}><SummaryCard label="Total latency" value={`${formatNumber(summary?.totalLatencyMs)} ms`} /></Col>
            <Col xs={12} md={8} xl={6}><SummaryCard label="Estimated tokens" value={formatNumber(summary?.totalTokens, 0)} /></Col>
            <Col xs={12} md={8} xl={6}><SummaryCard label="Tokens / second" value={formatNumber(summary?.tokensPerSecond)} /></Col>
            <Col xs={12} md={8} xl={6}><SummaryCard label="Estimated CO₂" value={`${formatNumber(summary?.estimatedCo2Grams, 6)} g`} /></Col>
            <Col xs={24} md={16} xl={12}><SummaryCard label="Engine" value={`${scorerDisplayName(summary?.engineName)}${summary?.engineVersion ? ` · ${summary.engineVersion}` : ""}`} /></Col>
          </Row>

          {summary?.engineName === "deepeval-deterministic" ? <Alert type="info" title="This deterministic local scorer reports approximate token- and claim-overlap indicators. It is not an LLM judge or an academic-grade correctness assessment." showIcon /> : null}

          <Card title="Metric summary">
            <Table rowKey="key" columns={metricColumns} dataSource={metricSummary} pagination={false} locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No valid metric results were returned" /> }} />
          </Card>

          <Card title="Case results">
            {caseItems.length ? <Collapse items={caseItems} /> : <Empty description="No case results" />}
            <Collapse ghost className="benchmark-run-advanced" items={[{ key: "advanced-run", label: "Advanced run details", children: <pre className="workspace-code">{JSON.stringify({ summary: safeParseJson(detail.summaryJson), systemMetrics: safeParseJson(detail.systemMetricsJson), rawResult: safeParseJson(detail.rawResultJson) }, null, 2)}</pre> }]} />
          </Card>
        </>
      ) : null}
    </div>
  );
}
