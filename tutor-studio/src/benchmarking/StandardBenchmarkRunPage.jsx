import { ArrowLeftOutlined, DeleteOutlined, ReloadOutlined, StopOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Descriptions, Flex, InputNumber, Progress, Space, Spin, Table, Tag, Typography } from "antd";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useSelector } from "react-redux";
import { getApiErrorDescription } from "../api/client.js";
import PageHeader from "../components/PageHeader.jsx";
import StandardBenchmarkRunLog from "./StandardBenchmarkRunLog.jsx";
import {
  cancelStandardBenchmarkRun,
  deleteStandardBenchmarkRun,
  getStandardBenchmarkRun,
  getStandardBenchmarkRunProgress,
  requeueStandardBenchmarkRun,
  setStandardBenchmarkRunPriority,
} from "./benchmarkingApi.js";
import { formatDateTime, formatPercent } from "./benchmarkingUtils.js";
import "./Benchmarking.css";

const POLL_INTERVAL_MS = 5_000;
const ACTIVE_STATUSES = ["Queued", "Running"];

const statusColor = (status) =>
  ({
    Queued: "default",
    Running: "processing",
    Completed: "success",
    Failed: "error",
    Cancelled: "warning",
  })[status] ?? "default";

// TaskScoresJson is a serialized list of { task, score } records.
const parseTaskScores = (json) => {
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
};

export default function StandardBenchmarkRunPage() {
  const { runId } = useParams();
  const navigate = useNavigate();
  const { roles = [] } = useSelector((state) => state.auth);
  const isAdmin = roles.includes("Admin");

  const [run, setRun] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [actionError, setActionError] = useState("");
  const [busy, setBusy] = useState("");
  const [priority, setPriority] = useState(0);

  const isActive = run !== null && ACTIVE_STATUSES.includes(run.status);

  useEffect(() => {
    if (!runId) return undefined;
    const controller = new AbortController();
    getStandardBenchmarkRun(runId, controller.signal)
      .then((data) => {
        setRun(data);
        setPriority(data?.priority ?? 0);
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load the benchmark run."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [runId]);

  // Poll the lightweight progress endpoint while active, then refetch the full
  // run once it settles so the per-task score table appears.
  useEffect(() => {
    if (!runId || !isActive) return undefined;
    const timer = setInterval(() => {
      const controller = new AbortController();
      getStandardBenchmarkRunProgress(runId, controller.signal)
        .then((progress) => setRun((current) => (current ? { ...current, ...progress } : current)))
        .catch(() => { /* transient poll failure; the next tick retries */ });
    }, POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [runId, isActive]);

  useEffect(() => {
    if (!runId || isActive || run?.taskScoresJson) return undefined;
    const controller = new AbortController();
    getStandardBenchmarkRun(runId, controller.signal)
      .then((data) => setRun((current) => (current ? { ...current, ...data } : current)))
      .catch(() => { /* keep the progress-derived run on failure */ });
    return () => controller.abort();
  }, [runId, isActive, run?.taskScoresJson]);

  const perform = async (label, action, after) => {
    setBusy(label);
    setActionError("");
    try {
      const outcome = await action();
      if (after) after(outcome);
      else {
        const refreshed = await getStandardBenchmarkRun(runId);
        setRun(refreshed);
      }
    } catch (requestError) {
      setActionError(getApiErrorDescription(requestError, "The action failed."));
    } finally {
      setBusy("");
    }
  };

  const progressValue = Math.max(0, Math.min(100, Number(run?.progressPercent) || 0));
  const hasCounts = Number(run?.totalTasks) > 0;

  if (loading) {
    return (
      <main className="workspace-page benchmarking-page">
        <div className="workspace-page-inner"><Spin size="large" /></div>
      </main>
    );
  }

  if (error || !run) {
    return (
      <main className="workspace-page benchmarking-page">
        <div className="workspace-page-inner">
          <Alert type="error" title={error || "Run not found."} showIcon />
          <Button style={{ marginTop: 16 }} onClick={() => navigate("/benchmarking?tab=history")}>Back to evaluations</Button>
        </div>
      </main>
    );
  }

  return (
    <main className="workspace-page benchmarking-page">
      <div className="workspace-page-inner">
        <Button type="link" icon={<ArrowLeftOutlined />} onClick={() => navigate("/benchmarking?tab=history")} style={{ paddingLeft: 0 }}>
          Back to evaluations
        </Button>
        <PageHeader title={`Evaluation · ${run.benchmarkKey}`} description="Live progress, results, and controls for one standardized benchmark run." />

        {actionError ? <Alert type="error" title={actionError} showIcon style={{ marginBottom: 16 }} /> : null}

        <Card size="small" style={{ marginBottom: 16 }}>
          <Flex justify="space-between" align="center" gap={16} wrap>
            <Space size={8}>
              <Tag color={statusColor(run.status)}>{run.status}</Tag>
              {run.progressStage ? <Typography.Text type="secondary">{run.progressStage}</Typography.Text> : null}
            </Space>
            {isActive ? (
              <Space size={8}>
                <Button icon={<ReloadOutlined />} onClick={() => perform("refresh", async () => {}, null)} loading={busy === "refresh"}>Refresh</Button>
                {isAdmin ? (
                  <Button danger icon={<StopOutlined />} onClick={() => perform("cancel", () => cancelStandardBenchmarkRun(runId))} loading={busy === "cancel"}>Cancel</Button>
                ) : null}
              </Space>
            ) : null}
            {!isActive && isAdmin ? (
              <Space size={8}>
                {(run.status === "Failed" || run.status === "Cancelled") ? (
                  <Button onClick={() => perform("requeue", () => requeueStandardBenchmarkRun(runId), (created) => navigate(`/benchmarking/runs/${created.id}`))} loading={busy === "requeue"}>Requeue</Button>
                ) : null}
                <Button danger icon={<DeleteOutlined />} onClick={() => perform("delete", () => deleteStandardBenchmarkRun(runId), () => navigate("/benchmarking?tab=history"))} loading={busy === "delete"}>Delete</Button>
              </Space>
            ) : null}
          </Flex>

          {isActive ? (
            <Progress
              percent={progressValue}
              status={run.status === "Running" ? "active" : "normal"}
              style={{ marginTop: 12 }}
              format={() => (hasCounts ? `${run.completedTasks ?? 0}/${run.totalTasks}` : `${progressValue.toFixed(0)}%`)}
            />
          ) : null}
        </Card>

        <Card size="small" title="Run details" style={{ marginBottom: 16 }}>
          <Descriptions bordered size="small" column={2}>
            <Descriptions.Item label="Suite">{run.benchmarkKey}</Descriptions.Item>
            <Descriptions.Item label="Model">{run.interactionModelName || "—"}</Descriptions.Item>
            <Descriptions.Item label="Overall score">{run.overallScore === null || run.overallScore === undefined ? "—" : formatPercent(run.overallScore)}</Descriptions.Item>
            <Descriptions.Item label="Tasks evaluated">{run.taskCount}</Descriptions.Item>
            <Descriptions.Item label="Progress">{hasCounts ? `${run.completedTasks ?? 0}/${run.totalTasks}` : "—"}</Descriptions.Item>
            <Descriptions.Item label="Priority">{run.priority ?? 0}</Descriptions.Item>
            <Descriptions.Item label="Few-shot examples">{run.nShots}</Descriptions.Item>
            <Descriptions.Item label="Problem cap">{run.nProblems === 0 ? "None (full dataset)" : `${run.nProblems} per task`}</Descriptions.Item>
            <Descriptions.Item label="Engine">{run.engineName ? `${run.engineName} ${run.engineVersion ?? ""}` : "—"}</Descriptions.Item>
            <Descriptions.Item label="Queued">{formatDateTime(run.queuedUtc)}</Descriptions.Item>
            <Descriptions.Item label="Started">{formatDateTime(run.startedUtc)}</Descriptions.Item>
            <Descriptions.Item label="Completed">{formatDateTime(run.completedUtc || run.cancelledUtc)}</Descriptions.Item>
            {run.notes ? <Descriptions.Item label="Notes" span={2}>{run.notes}</Descriptions.Item> : null}
            {run.failureCode ? <Descriptions.Item label="Failure" span={2}>{run.failureCode}: {run.failureDescription}</Descriptions.Item> : null}
          </Descriptions>
        </Card>

        {run.status === "Queued" && isAdmin ? (
          <Card size="small" title="Queue priority" style={{ marginBottom: 16 }}>
            <Flex align="center" gap={12} wrap>
              <Typography.Text type="secondary">Lower values run first.</Typography.Text>
              <InputNumber value={priority} onChange={(value) => setPriority(value ?? 0)} />
              <Button
                type="primary"
                loading={busy === "priority"}
                onClick={() => perform("priority", () => setStandardBenchmarkRunPriority(runId, priority))}
              >
                Set priority
              </Button>
            </Flex>
          </Card>
        ) : null}

        {run.taskScoresJson ? (
          <Card size="small" title="Per-task scores" style={{ marginBottom: 16 }}>
            <Table
              rowKey="task"
              size="small"
              pagination={false}
              dataSource={parseTaskScores(run.taskScoresJson)}
              columns={[
                { title: "Task", dataIndex: "task" },
                { title: "Score", dataIndex: "score", width: 120, render: (value) => formatPercent(value) },
              ]}
            />
          </Card>
        ) : null}

        <StandardBenchmarkRunLog runId={runId} />
      </div>
    </main>
  );
}
