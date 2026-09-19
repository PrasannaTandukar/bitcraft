import { ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Descriptions, Drawer, Empty, Flex, Table, Tag, Typography } from "antd";
import { useEffect, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { getStandardBenchmarkRun, listStandardBenchmarkRuns } from "./benchmarkingApi.js";
import { formatDateTime, formatPercent } from "./benchmarkingUtils.js";

const statusColor = (status) => ({ Queued: "default", Running: "processing", Completed: "success", Failed: "error" }[status] ?? "default");

export default function StandardBenchmarkHistory({ selectedRunId, onOpenRun, onCloseRun }) {
  const [runs, setRuns] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [refreshNonce, setRefreshNonce] = useState(0);
  const [detail, setDetail] = useState(null);
  const [detailOpen, setDetailOpen] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    listStandardBenchmarkRuns(null, controller.signal)
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
  }, [refreshNonce]);

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
                <pre style={{ margin: 0, fontSize: 12, whiteSpace: "pre-wrap" }}>{detail.taskScoresJson}</pre>
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
