import { Tag } from "antd";

function statusColor(value) {
  const status = String(value ?? "").toLowerCase();

  if (["active", "enabled", "completed", "complete", "vectorized", "passed", "pass", "ready"].some((item) => status.includes(item))) {
    return "success";
  }
  if (["running", "processing", "published", "in progress"].some((item) => status.includes(item))) {
    return "processing";
  }
  if (["fail", "error", "rejected"].some((item) => status.includes(item))) {
    return "error";
  }
  if (["warning", "stale", "inactive", "not ready"].some((item) => status.includes(item))) {
    return "warning";
  }
  return "default";
}

function StatusTag({ value, children }) {
  return <Tag color={statusColor(value)}>{children ?? value ?? "Unknown"}</Tag>;
}

export default StatusTag;
