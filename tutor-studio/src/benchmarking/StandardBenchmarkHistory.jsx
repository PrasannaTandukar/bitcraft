import { Descriptions, Drawer, Table, Tag, Typography } from "antd";
import { formatDateTime, formatPercent } from "./benchmarkingUtils.js";

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

export function StandardBenchmarkRunDrawer({ detail, open, onClose }) {
  return (
    <Drawer
      title={detail ? `Standardized run · ${detail.benchmarkKey}` : "Standardized run"}
      open={open}
      onClose={onClose}
      size={560}
    >
        {detail ? (
          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label="Status"><Tag color={statusColor(detail.status)}>{detail.status}</Tag></Descriptions.Item>
            {detail.progressStage ? <Descriptions.Item label="Stage">{detail.progressStage}</Descriptions.Item> : null}
            {detail.totalTasks ? <Descriptions.Item label="Progress">{`${detail.completedTasks ?? 0}/${detail.totalTasks}`}{detail.progressPercent ? ` (${Number(detail.progressPercent).toFixed(0)}%)` : ""}</Descriptions.Item> : null}
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
  );
}

export default StandardBenchmarkRunDrawer;
