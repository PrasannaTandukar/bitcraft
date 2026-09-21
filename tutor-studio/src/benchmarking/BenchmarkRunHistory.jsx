import { ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Empty, Flex, Form, Select, Space, Table, Typography } from "antd";
import InfoTooltip from "../components/InfoTooltip.jsx";
import BenchmarkingHelp from "./BenchmarkingHelp.jsx";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getApiErrorDescription } from "../api/client.js";
import StatusTag from "../components/StatusTag.jsx";
import BenchmarkRunDetails from "./BenchmarkRunDetails.jsx";
import { listBenchmarkRuns, listStandardBenchmarkRuns } from "./benchmarkingApi.js";
import { formatDateTime, formatPercent, toHistoryRow } from "./benchmarkingUtils.js";

const POLL_INTERVAL_MS = 10_000;
const isActiveStatus = (status) => status === "Queued" || status === "Running";

export default function BenchmarkRunHistory({ tutors, tutorsLoading, tutorsError, selectedTutorId, selectedRunId, onSelectTutor, onOpenRun, onCloseRun }) {
  const [runs, setRuns] = useState([]);
  const [standardRuns, setStandardRuns] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [refreshNonce, setRefreshNonce] = useState(0);
  const navigate = useNavigate();

  useEffect(() => {
    const controller = new AbortController();
    const standardRequest = listStandardBenchmarkRuns(null, controller.signal)
      .then((data) => setStandardRuns(data ?? []))
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load standardized benchmark runs."));
      });
    const tutorRequest = selectedTutorId
      ? listBenchmarkRuns(selectedTutorId, controller.signal)
          .then((data) => setRuns(data ?? []))
          .catch((requestError) => {
            if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load benchmark run history."));
          })
      : Promise.resolve();
    Promise.all([standardRequest, tutorRequest]).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [refreshNonce, selectedTutorId]);

  const rows = [
    ...runs.map((run) => toHistoryRow(run, "tutor")),
    ...standardRuns.map((run) => toHistoryRow(run, "standard")),
  ].sort((a, b) => new Date(b.completedUtc || b.queuedUtc) - new Date(a.completedUtc || a.queuedUtc));

  // Poll both lists while any run is queued or running so the table reflects
  // stage transitions without a manual refresh. Stops when all runs settle.
  const anyActive = rows.some((row) => isActiveStatus(row.status));
  useEffect(() => {
    if (!anyActive) return undefined;
    const timer = setInterval(() => setRefreshNonce((value) => value + 1), POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [anyActive]);

  const selectedRow = rows.find((row) => row.id === selectedRunId) ?? null;

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

  // Standard runs drill down to their own page (D-2). Tutor runs keep the drawer.
  const openRow = (row) => {
    if (row.kind === "standard") {
      navigate(`/benchmarking/runs/${row.id}`);
      return;
    }
    onOpenRun(row.id);
  };

  if (selectedRow?.kind === "tutor" && selectedTutorId) return <BenchmarkRunDetails tutorId={selectedTutorId} runId={selectedRunId} initialRun={selectedRow.raw} onBack={closeRun} />;

  const columns = [
    {
      title: "Status",
      key: "status",
      width: 120,
      render: (_, row) => (
        <div>
          <InfoTooltip title="Run progress" text={row.kind === "tutor" ? "Completed cases out of total cases. The stage name appears when the engine reports no counts." : "Completed problems out of total problems. The stage name appears when the engine reports no counts."}>
            <StatusTag value={row.statusLabel} />
          </InfoTooltip>
          {row.failureDescription ? <Typography.Text type="danger" ellipsis={{ tooltip: row.failureDescription }} className="benchmark-failure-cell">{row.failureDescription}</Typography.Text> : null}
        </div>
      ),
    },
    {
      title: "Type",
      dataIndex: "kind",
      width: 90,
      render: (value) => (value === "standard" ? "Standard" : "Tutor"),
    },
    {
      title: "Revision",
      key: "revision",
      width: 110,
      render: (_, row) => (row.kind !== "tutor" ? "—" : row.raw.tutorRevisionNumber ? `Rev ${row.raw.tutorRevisionNumber}` : row.raw.tutorRevisionId ? "Recorded" : "Legacy"),
    },
    {
      title: "Model",
      dataIndex: "model",
      width: 140,
      ellipsis: true,
      render: (value) => value || "—",
    },
    {
      title: "Template / Suite",
      dataIndex: "target",
      width: 150,
      ellipsis: true,
      render: (value) => value || "—",
    },
    {
      title: "Score",
      key: "score",
      width: 120,
      render: (_, row) => (
        <Space size={4} split={<Typography.Text orientation="vertical" type="secondary" style={{ fontSize: 11 }}>/</Typography.Text>}>
          <InfoTooltip title="Average score" text={row.kind === "tutor" ? "Mean case score across all evaluated cases." : "Overall accuracy reported by the benchmark engine."}>
            <span orientation="horizontal">{formatPercent(row.score)}</span>
          </InfoTooltip>
          {row.kind === "tutor" ? (
            <InfoTooltip title="Pass rate" text="Share of cases whose score met or exceeded the template threshold.">
              <span style={{ color: "var(--bitcraft-color-text-secondary)", fontSize: 12 }}>{formatPercent(row.raw.passRate)} pass</span>
            </InfoTooltip>
          ) : null}
        </Space>
      ),
    },
    {
      title: "Cases / Tasks",
      dataIndex: "count",
      width: 90,
      align: "center",
      render: (val) => val ?? "—",
    },
    {
      title: "Completed",
      dataIndex: "completedUtc",
      width: 150,
      sorter: (a, b) => new Date(a.completedUtc || a.queuedUtc) - new Date(b.completedUtc || b.queuedUtc),
      render: (val, row) => formatDateTime(val || row.queuedUtc),
    },
    {
      title: "",
      key: "action",
      fixed: "right",
      width: 90,
      render: (_, row) => <Button type="link" size="small" onClick={() => openRow(row)}>View run</Button>,
    },
  ];

  return (
    <div className="benchmark-panel-stack">
      <BenchmarkingHelp
        title="How evaluations work"
        steps={[
          "Launch a run from the New run tab.",
          "The backend queues the run and starts it when the engine is free.",
          "This table refreshes every 10 seconds while any run is active.",
          "Double-click a row to open its detail drawer.",
        ]}
        note="Standard runs appear without a tutor filter. Select a tutor to add its tutor runs to this list."
      />
      <Flex justify="space-between" align="flex-end" gap={16} wrap>
        <Form.Item label="Tutor" className="benchmark-tutor-filter">
          <Select showSearch optionFilterProp="label" value={selectedTutorId || undefined} onChange={selectTutor} loading={tutorsLoading} placeholder="Select a tutor" options={tutors.map((tutor) => ({ value: tutor.id, label: tutor.name }))} />
        </Form.Item>
        <Button icon={<ReloadOutlined />} onClick={refresh} loading={loading}>Refresh evaluations</Button>
      </Flex>
      {tutorsError ? <Alert type="error" title={tutorsError} showIcon /> : null}
      {error ? <Alert type="error" title={error} showIcon /> : null}
      <Table
        className="workspace-table benchmark-run-history-table"
        rowKey="id"
        columns={columns}
        dataSource={rows}
        loading={loading}
        size="middle"
        pagination={{ pageSize: 10, showSizeChanger: true }}
        locale={{ emptyText: <Empty description={selectedTutorId ? "No benchmark runs for this tutor" : "Select a tutor to view its runs. Standard runs appear without a tutor filter."} /> }}
        onRow={(row) => ({ onDoubleClick: () => openRow(row) })}
      />
    </div>
  );
}
