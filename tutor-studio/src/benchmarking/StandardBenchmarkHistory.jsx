import { ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Descriptions, Drawer, Empty, Flex, Table, Tag, Typography } from "antd";
import { useCallback, useEffect, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { getStandardBenchmarkRun, getStandardBenchmarkRunProgress, listStandardBenchmarkRuns } from "./benchmarkingApi.js";
import { formatDateTime, formatPercent } from "./benchmarkingUtils.js";

const POLL_INTERVAL_MS = 10_000;
const isActiveStatus = (status) => status === "Queued" || status === "Running";

// TaskScoresJson is a serialized list of { task, score } records.
const parseTaskScores = (json) => {
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
};

const statusColor = (status) => ({ Queued: "default", Running: "processing", Completed: "success", Failed: "error" }[status] ?? "default");

export default function StandardBenchmarkHistory({ selectedRunId, onOpenRun, onCloseRun }) {
  const [runs, setRuns] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [refreshNonce, setRefreshNonce] = useState(0);
  const [detail, setDetail] = useState(null);
  const [detailOpen, setDetailOpen] = useState(false);

  const loadRuns = useCallback((signal) => listStandardBenchmarkRuns(null, signal), []);

  useEffect(() => {
    const controller = new AbortController();
    loadRuns(controller.signal)
      .then((data) => {
        setRuns(data ?? []);
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load standardized benchmark runs."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [refreshNonce, loadRuns]);

  // Poll the list while any run is queued or running so the table reflects
  // stage transitions without a manual refresh. Stops when all runs settle.
  const anyActive = runs.some((run) => isActiveStatus(run.status));
  useEffect(() => {
    if (!anyActive) return undefined;
    const timer = setInterval(() => {
      const controller = new AbortController();
      loadRuns(controller.signal)
        .then((data) => setRuns(data ?? []))
        .catch(() => { /* transient poll failure; next tick retries */ });
    }, POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [anyActive, loadRuns]);

  useEffect(() => {
    if (!selectedRunId) return undefined;
    const controller = new AbortController();
    getStandardBenchmarkRun(selectedRunId, controller.signal)
      .then((data) => {
        setDetail(data);
        setDetailOpen(true);
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load the standardized benchmark run."));
      });
    return () => controller.abort();
  }, [selectedRunId]);

  // While the drawer is open on an active run, poll the lightweight progress
  // endpoint so stage and score update live.
  const detailActive = detail !== null && isActiveStatus(detail.status);
  useEffect(() => {
    if (!selectedRunId || !detailActive) return undefined;
    const timer = setInterval(() => {
      const controller = new AbortController();
      getStandardBenchmarkRunProgress(selectedRunId, controller.signal)
        .then((progress) => setDetail((current) => (current ? { ...current, ...progress } : current)))
        .catch(() => { /* transient poll failure; next tick retries */ });
    }, POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [selectedRunId, detailActive]);

  // The progress response is deliberately lightweight and omits taskScoresJson.
  // When the run reaches a terminal state, refetch the full run once so the
  // drawer shows the per-task score table (AC19).
  const detailSettled = detail !== null && !isActiveStatus(detail.status);
  useEffect(() => {
    if (!selectedRunId || !detailSettled || detail.taskScoresJson) return undefined;
    const controller = new AbortController();
    getStandardBenchmarkRun(selectedRunId, controller.signal)
      .then((data) => setDetail((current) => (current ? { ...current, ...data } : current)))
      .catch(() => { /* keep the progress-derived detail on failure */ });
    return () => controller.abort();
  }, [selectedRunId, detailSettled, detail?.taskScoresJson]);

  const closeDetail = () => {
    setDetailOpen(false);
    setRefreshNonce((value) => value + 1);
    onCloseRun();
  };

  const columns = [
    {
      title: "Status",
      dataIndex: "status",
      width: 110,
      render: (value) => <Tag color={statusColor(value)}>{value}</Tag>,
    },
    {
      title: "Suite",
      dataIndex: "benchmarkKey",
      width: 150,
      render: (value) => <Typography.Text strong>{value}</Typography.Text>,
    },
    {
      title: "Model",
      key: "model",
      render: (_, run) => (
        <div>
          <Typography.Text strong>{run.interactionModelName || "Unknown model"}</Typography.Text>
        </div>
      ),
    },
    { title: "Overall score", dataIndex: "overallScore", width: 120, render: (value) => (value === null || value === undefined) ? "—" : formatPercent(value) },
    { title: "Stage", dataIndex: "progressStage", width: 130, render: (value) => value ?? "—" },
    { title: "Tasks", dataIndex: "taskCount", width: 80 },
    { title: "Problems", dataIndex: "nProblems", width: 90, render: (value) => (value === 0 ? "All" : value) },
    { title: "Shots", dataIndex: "nShots", width: 70 },
    { title: "Engine", key: "engine", width: 160, render: (_, run) => run.engineName ? `${run.engineName} ${run.engineVersion ?? ""}` : "—" },
    { title: "Queued", dataIndex: "queuedUtc", width: 170, render: (value) => formatDateTime(value) },
  ];

  return (
    <div>
      <Flex justify="space-between" align="center" style={{ marginBottom: 12 }}>
        <Typography.Text type="secondary">Model-scoped standardized benchmark runs (MMLU, GSM8K, …) executed via the DeepEval sidecar.</Typography.Text>
        <Button icon={<ReloadOutlined />} onClick={() => setRefreshNonce((value) => value + 1)} loading={loading}>Refresh</Button>
      </Flex>
      {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
      <Table
        rowKey="id"
        columns={columns}
        dataSource={runs}
        loading={loading}
        size="middle"
        pagination={{ pageSize: 10, showSizeChanger: true }}
        locale={{ emptyText: <Empty description="No standardized benchmark runs yet" /> }}
        onRow={(record) => ({ onClick: () => onOpenRun(record.id), style: { cursor: "pointer" } })}
      />
      <Drawer
        title={detail ? `Standardized run · ${detail.benchmarkKey}` : "Standardized run"}
        open={detailOpen}
        onClose={closeDetail}
        size={560}
      >
        {detail ? (
          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label="Status"><Tag color={statusColor(detail.status)}>{detail.status}</Tag></Descriptions.Item>
            {detail.progressStage ? <Descriptions.Item label="Stage">{detail.progressStage}</Descriptions.Item> : null}
            <Descriptions.Item label="Model">{detail.interactionModelName || "—"}</Descriptions.Item>
            <Descriptions.Item label="Suite">{detail.benchmarkKey}</Descriptions.Item>
            <Descriptions.Item label="Overall score">{(detail.overallScore === null || detail.overallScore === undefined) ? "—" : formatPercent(detail.overallScore)}</Descriptions.Item>
            <Descriptions.Item label="Tasks evaluated">{detail.taskCount}</Descriptions.Item>
            <Descriptions.Item label="Problem cap">{detail.nProblems === 0 ? "None (full dataset)" : `${detail.nProblems} per task`}</Descriptions.Item>
            <Descriptions.Item label="Few-shot examples">{detail.nShots}</Descriptions.Item>
            <Descriptions.Item label="Engine">{detail.engineName ? `${detail.engineName} ${detail.engineVersion ?? ""}` : "—"}</Descriptions.Item>
            <Descriptions.Item label="Queued">{formatDateTime(detail.queuedUtc)}</Descriptions.Item>
            <Descriptions.Item label="Completed">{detail.completedUtc ? formatDateTime(detail.completedUtc) : "—"}</Descriptions.Item>
            {detail.notes ? <Descriptions.Item label="Notes">{detail.notes}</Descriptions.Item> : null}
            {detail.failureCode ? <Descriptions.Item label="Failure">{detail.failureCode}: {detail.failureDescription}</Descriptions.Item> : null}
            {detail.taskScoresJson ? (
              <Descriptions.Item label="Task scores">
                <Table
                  rowKey="task"
                  size="small"
                  pagination={false}
                  dataSource={parseTaskScores(detail.taskScoresJson)}
                  columns={[
                    { title: "Task", dataIndex: "task" },
                    { title: "Score", dataIndex: "score", width: 90, render: (value) => formatPercent(value) },
                  ]}
                />
              </Descriptions.Item>
            ) : null}
          </Descriptions>
        ) : (
          <Typography.Text type="secondary">Loading run…</Typography.Text>
        )}
      </Drawer>
    </div>
  );
}
