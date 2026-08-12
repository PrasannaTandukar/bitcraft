import { ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Empty, Flex, Form, Select, Table, Typography } from "antd";
import { useEffect, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import StatusTag from "../components/StatusTag.jsx";
import BenchmarkRunDetails from "./BenchmarkRunDetails.jsx";
import { listBenchmarkRuns } from "./benchmarkingApi.js";
import { formatDateTime, formatPercent, runStatusLabel, scorerDisplayName } from "./benchmarkingUtils.js";

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
    { title: "Status", key: "status", width: 140, render: (_, run) => <div><StatusTag value={runStatusLabel(run)} />{run.failureDescription ? <Typography.Text type="danger" className="benchmark-failure-cell">{run.failureDescription}</Typography.Text> : null}</div> },
    { title: "Revision", key: "revision", width: 130, render: (_, run) => run.tutorRevisionNumber ? `Revision ${run.tutorRevisionNumber}` : run.tutorRevisionId ? "Recorded" : "Legacy" },
    { title: "Model", dataIndex: "modelAlias", width: 160, render: (value) => value || "—" },
    { title: "Template", dataIndex: "benchmarkTemplateName", width: 180, render: (value) => value || "Template" },
    { title: "Queued", dataIndex: "queuedUtc", width: 180, sorter: (a, b) => new Date(a.queuedUtc) - new Date(b.queuedUtc), render: formatDateTime },
    { title: "Completed", dataIndex: "completedUtc", width: 180, render: formatDateTime },
    { title: "Cases", dataIndex: "totalCaseCount", width: 85 },
    { title: "Average", dataIndex: "averageScore", width: 100, render: formatPercent },
    { title: "Pass rate", dataIndex: "passRate", width: 100, render: formatPercent },
    { title: "Engine", dataIndex: "engineName", width: 180, render: scorerDisplayName },
    { title: "", key: "action", fixed: "right", width: 100, render: (_, run) => <Button type="link" onClick={() => onOpenRun(run.id)}>View run</Button> },
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
        className="workspace-table"
        rowKey="id"
        columns={columns}
        dataSource={runs}
        loading={loading}
        scroll={{ x: 1300 }}
        pagination={{ pageSize: 10, showSizeChanger: true }}
        locale={{ emptyText: <Empty description={selectedTutorId ? "This tutor has no benchmark runs" : "Select a tutor to view its runs"} /> }}
        onRow={(run) => ({ onDoubleClick: () => onOpenRun(run.id) })}
      />
    </div>
  );
}
