import { PauseCircleOutlined, PlayCircleOutlined, ReloadOutlined, ClearOutlined, DeleteOutlined, StopOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Empty, Flex, InputNumber, Popconfirm, Space, Switch, Table, Tag, Tooltip, Typography, message } from "antd";
import { useCallback, useEffect, useState } from "react";
import { useSelector } from "react-redux";
import { getApiErrorDescription } from "../api/client.js";
import StatusTag from "../components/StatusTag.jsx";
import {
  archiveStandardBenchmarkRun,
  cancelStandardBenchmarkRun,
  clearBenchmarkEngineSlot,
  deleteStandardBenchmarkRun,
  getBenchmarkEngineStatus,
  getBenchmarkQueuePaused,
  listStandardBenchmarkRuns,
  requeueStandardBenchmarkRun,
  setBenchmarkQueuePaused,
  setStandardBenchmarkRunPriority,
} from "./benchmarkingApi.js";
import { formatDateTime } from "./benchmarkingUtils.js";

const POLL_INTERVAL_MS = 5_000;

// Queue panel (HD-4/HD-8/HD-9): position, actions, engine status. Status is
// readable for Auditor+; every action is Admin-only.
export default function BenchmarkQueuePanel() {
  const { roles = [] } = useSelector((state) => state.auth);
  const isAdmin = roles.includes("Admin");

  const [runs, setRuns] = useState([]);
  const [paused, setPaused] = useState(false);
  const [engine, setEngine] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState("");
  const [priorityDrafts, setPriorityDrafts] = useState({});
  const [messageApi, contextHolder] = message.useMessage();

  const active = (run) => run.status === "Queued" || run.status === "Running";

  const load = useCallback(
    (signal) => {
      const runsRequest = listStandardBenchmarkRuns(null, signal)
        .then((data) => setRuns(data ?? []))
        .catch((requestError) => {
          if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load the benchmark queue."));
        });
      const pausedRequest = getBenchmarkQueuePaused(signal)
        .then((data) => setPaused(Boolean(data?.isPaused)))
        .catch(() => { /* non-admin still sees the queue; keep last state */ });
      const engineRequest = getBenchmarkEngineStatus(signal)
        .then((data) => setEngine(data ?? null))
        .catch(() => setEngine(null));
      return Promise.all([runsRequest, pausedRequest, engineRequest]).finally(() => setLoading(false));
    },
    []
  );

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  // Poll while any run is queued or running, or while the queue is paused, so
  // a resume reflects promptly even without user interaction.
  const anyActive = runs.some(active);
  useEffect(() => {
    if (!anyActive && !paused) return undefined;
    const timer = setInterval(() => {
      const controller = new AbortController();
      load(controller.signal);
      return () => controller.abort();
    }, POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [anyActive, paused, load]);

  const perform = async (label, action, successText) => {
    setBusy(label);
    setError("");
    try {
      await action();
      if (successText) messageApi.success(successText);
      const controller = new AbortController();
      await load(controller.signal);
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "The action failed."));
    } finally {
      setBusy("");
    }
  };

  const togglePause = (next) =>
    perform("pause", () => setBenchmarkQueuePaused(next), next ? "Queue paused. Running runs finish." : "Queue resumed.");

  const clearSlot = () => perform("clear-slot", () => clearBenchmarkEngineSlot(), "Runner slot cleared.");

  const queueRows = runs
    .filter((run) => !run.isArchived && (active(run) || run.status === "Failed" || run.status === "Cancelled"))
    .sort((a, b) => {
      const activeRank = (row) => (row.status === "Running" ? 0 : row.status === "Queued" ? 1 : 2);
      const rank = activeRank(a) - activeRank(b);
      if (rank !== 0) return rank;
      return (a.priority ?? 0) - (b.priority ?? 0) || new Date(a.queuedUtc) - new Date(b.queuedUtc);
    })
    .map((run, index) => ({ ...run, position: run.status === "Queued" ? index + 1 : null }));

  const columns = [
    {
      title: "#",
      dataIndex: "position",
      width: 45,
      render: (value) => value ?? "—",
    },
    {
      title: "Status",
      dataIndex: "status",
      width: 110,
      render: (value, run) => (
        <Space size={0} direction="vertical">
          <StatusTag value={value} />
          {run.progressStage ? <Typography.Text type="secondary" style={{ fontSize: 12 }}>{run.progressStage}</Typography.Text> : null}
        </Space>
      ),
    },
    { title: "Suite", dataIndex: "benchmarkKey", width: 130 },
    { title: "Model", dataIndex: "interactionModelName", width: 150, ellipsis: true, render: (value) => value || "—" },
    {
      title: "Priority",
      dataIndex: "priority",
      width: 120,
      render: (value, run) =>
        isAdmin && run.status === "Queued" ? (
          <Space size={4}>
            <InputNumber
              size="small"
              value={priorityDrafts[run.id] ?? value ?? 0}
              onChange={(next) => setPriorityDrafts((drafts) => ({ ...drafts, [run.id]: next ?? 0 }))}
              style={{ width: 70 }}
            />
            <Button
              size="small"
              type="link"
              loading={busy === `priority-${run.id}`}
              onClick={() =>
                perform(`priority-${run.id}`, () =>
                  setStandardBenchmarkRunPriority(run.id, priorityDrafts[run.id] ?? run.priority ?? 0)
                )
              }
            >
              Set
            </Button>
          </Space>
        ) : (
          value ?? 0
        ),
    },
    {
      title: "Queued",
      dataIndex: "queuedUtc",
      width: 150,
      render: (value) => formatDateTime(value),
    },
    {
      title: "",
      key: "actions",
      fixed: "right",
      width: 220,
      render: (_, run) =>
        isAdmin ? (
          <Space size={0}>
            {active(run) ? (
              <Popconfirm title="Cancel this run?" onConfirm={() => perform(`cancel-${run.id}`, () => cancelStandardBenchmarkRun(run.id), "Run cancelled.")}>
                <Button size="small" type="text" danger icon={<StopOutlined />} loading={busy === `cancel-${run.id}`} />
              </Popconfirm>
            ) : null}
            {run.status === "Failed" || run.status === "Cancelled" ? (
              <Tooltip title="Requeue">
                <Button size="small" type="text" loading={busy === `requeue-${run.id}`} onClick={() => perform(`requeue-${run.id}`, () => requeueStandardBenchmarkRun(run.id), "Run requeued.")}>
                  Requeue
                </Button>
              </Tooltip>
            ) : null}
            {!active(run) ? (
              <>
                <Tooltip title="Archive">
                  <Button size="small" type="text" loading={busy === `archive-${run.id}`} onClick={() => perform(`archive-${run.id}`, () => archiveStandardBenchmarkRun(run.id), "Run archived.")}>
                    Archive
                  </Button>
                </Tooltip>
                <Popconfirm title="Delete this run permanently?" onConfirm={() => perform(`delete-${run.id}`, () => deleteStandardBenchmarkRun(run.id), "Run deleted.")}>
                  <Button size="small" type="text" danger icon={<DeleteOutlined />} loading={busy === `delete-${run.id}`} />
                </Popconfirm>
              </>
            ) : null}
          </Space>
        ) : null,
    },
  ];

  const engineStatus = engine ? (
    <Card size="small" title="Sidecar engine" style={{ marginBottom: 16 }} data-testid="benchmark-engine-status">
      <Flex justify="space-between" align="center" gap={16} wrap>
        <Space size={16} wrap>
          <Tag color={engine.isReachable ? "success" : "error"}>{engine.isReachable ? "Reachable" : "Unreachable"}</Tag>
          <Typography.Text type="secondary">transport: {engine.transport ?? "—"}</Typography.Text>
          <Tag color={engine.slotHeld ? "processing" : "default"}>{engine.slotHeld ? `Slot held (${engine.currentRunId ?? "unknown run"})` : "Slot free"}</Tag>
          {engine.engineVersion ? <Typography.Text type="secondary">deepeval {engine.engineVersion}</Typography.Text> : null}
          {engine.lastError ? <Typography.Text type="danger" ellipsis={{ tooltip: engine.lastError }}>{engine.lastError}</Typography.Text> : null}
        </Space>
        {isAdmin && engine.slotHeld ? (
          <Popconfirm title="Clear the runner slot? A dead runner can hold it forever." onConfirm={clearSlot}>
            <Button icon={<ClearOutlined />} loading={busy === "clear-slot"}>Clear slot</Button>
          </Popconfirm>
        ) : null}
      </Flex>
    </Card>
  ) : null;

  return (
    <div className="benchmark-panel-stack">
      {contextHolder}
      {error ? <Alert type="error" title={error} showIcon /> : null}
      {engineStatus}
      <Card size="small" title="Queue">
        <Flex justify="space-between" align="center" gap={16} wrap style={{ marginBottom: 12 }}>
          <Space size={8}>
            {isAdmin ? (
              <Switch
                checkedChildren={<PauseCircleOutlined />}
                unCheckedChildren={<PlayCircleOutlined />}
                checked={paused}
                onChange={togglePause}
                loading={busy === "pause"}
              />
            ) : null}
            <Typography.Text>
              {paused ? "Queue paused — new runs wait; running runs finish." : "Queue active."}
            </Typography.Text>
          </Space>
          <Button icon={<ReloadOutlined />} onClick={() => { setLoading(true); const controller = new AbortController(); load(controller.signal); }} loading={loading}>
            Refresh
          </Button>
        </Flex>
        <Table
          className="workspace-table"
          rowKey="id"
          size="small"
          columns={columns}
          dataSource={queueRows}
          loading={loading}
          pagination={false}
          locale={{ emptyText: <Empty description="No queued, running, failed, or cancelled benchmark runs." /> }}
        />
      </Card>
    </div>
  );
}
