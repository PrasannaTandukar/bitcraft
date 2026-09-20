import { ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Empty, Flex, Form, Select, Space, Table, Typography } from "antd";
import { useEffect, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import StatusTag from "../components/StatusTag.jsx";
import BenchmarkRunDetails from "./BenchmarkRunDetails.jsx";
import { listBenchmarkRuns } from "./benchmarkingApi.js";
import { formatDateTime, formatPercent, runStatusLabel } from "./benchmarkingUtils.js";

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
  }, [refreshNonce, selectedTutorId]);

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
  if (selectedTutorId && selectedRunId) return <BenchmarkRunDetails tutorId={selectedTutorId} runId={selectedRunId} initialRun={selectedRun} onBack={closeRun} />;

  const columns = [
    {
      title: "Status",
      key: "status",
      width: 120,
      render: (_, run) => (
        <div>
          <StatusTag value={runStatusLabel(run)} />
          {run.failureDescription ? <Typography.Text type="danger" ellipsis={{ tooltip: run.failureDescription }} className="benchmark-failure-cell">{run.failureDescription}</Typography.Text> : null}
        </div>
      ),
    },
    {
      title: "Revision",
      key: "revision",
      width: 110,
      render: (_, run) => (run.tutorRevisionNumber ? `Rev ${run.tutorRevisionNumber}` : run.tutorRevisionId ? "Recorded" : "Legacy"),
    },
    {
      title: "Model",
      dataIndex: "modelAlias",
      width: 140,
      ellipsis: true,
      render: (value) => value || "—",
    },
    {
      title: "Template",
      dataIndex: "benchmarkTemplateName",
      width: 150,
      ellipsis: true,
      render: (value) => value || "Template",
    },
    {
      title: "Score",
      key: "score",
      width: 120,
      render: (_, run) => (
        <Space size={4} split={<Typography.Text orientation="vertical" type="secondary" style={{ fontSize: 11 }}>/</Typography.Text>}>
          <span orientation="horizontal" title="Average score">{formatPercent(run.averageScore)}</span>
          <span style={{ color: "var(--bitcraft-color-text-secondary)", fontSize: 12 }} title="Pass rate">{formatPercent(run.passRate)} pass</span>
        </Space>
      ),
    },
    {
      title: "Cases",
      dataIndex: "totalCaseCount",
      width: 70,
      align: "center",
      render: (val) => val ?? "—",
    },
    {
      title: "Completed",
      dataIndex: "completedUtc",
      width: 150,
      sorter: (a, b) => new Date(a.completedUtc || a.queuedUtc) - new Date(b.completedUtc || b.queuedUtc),
      render: (val, run) => formatDateTime(val || run.queuedUtc),
    },
    {
      title: "",
      key: "action",
      fixed: "right",
      width: 90,
      render: (_, run) => <Button type="link" size="small" onClick={() => onOpenRun(run.id)}>View run</Button>,
    },
  ];

  return (
    <div className="benchmark-panel-stack">
      <Flex justify="space-between" align="flex-end" gap={16} wrap>
        <Form.Item label="Tutor" className="benchmark-tutor-filter">
          <Select showSearch optionFilterProp="label" value={selectedTutorId || undefined} onChange={selectTutor} loading={tutorsLoading} placeholder="Select a tutor" options={tutors.map((tutor) => ({ value: tutor.id, label: tutor.name }))} />
        </Form.Item>
        <Button icon={<ReloadOutlined />} onClick={refresh} disabled={!selectedTutorId} loading={loading}>Refresh history</Button>
      </Flex>
      {tutorsError ? <Alert type="error" title={tutorsError} showIcon /> : null}
      {error ? <Alert type="error" title={error} showIcon /> : null}
      <Table
        className="workspace-table benchmark-run-history-table"
        rowKey="id"
        columns={columns}
        dataSource={runs}
        loading={loading}
        size="middle"
        pagination={{ pageSize: 10, showSizeChanger: true }}
        locale={{ emptyText: <Empty description={selectedTutorId ? "This tutor has no benchmark runs" : "Select a tutor to view its runs"} /> }}
        onRow={(run) => ({ onDoubleClick: () => onOpenRun(run.id) })}
      />
    </div>
  );
}
