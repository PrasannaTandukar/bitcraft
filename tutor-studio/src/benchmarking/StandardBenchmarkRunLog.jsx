import { ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Empty, Flex, Segmented, Typography } from "antd";
import { useMemo, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { getStandardBenchmarkRunLog } from "./benchmarkingApi.js";

// tqdm writes carriage-return progress bars that flood the log with near-duplicate
// lines. Drop them so the view shows decisions and errors, not redraw frames.
const isNoise = (line) => {
  const trimmed = line.trim();
  if (!trimmed) return true;
  if (trimmed.includes("\r")) return true;
  if (/^\d+%\|/.test(trimmed)) return true;
  if (/it\/s\]$/.test(trimmed)) return true;
  return false;
};

const isErrorLine = (line) =>
  /\b(error|exception|traceback|failed|failure)\b/i.test(line);

export default function StandardBenchmarkRunLog({ runId }) {
  const [log, setLog] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [level, setLevel] = useState("all");

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const data = await getStandardBenchmarkRunLog(runId);
      setLog(data?.log ?? "");
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not load the run log."));
    } finally {
      setLoading(false);
    }
  };

  const lines = useMemo(() => {
    if (!log) return [];
    const kept = log.split("\n").filter((line) => !isNoise(line));
    return level === "errors" ? kept.filter(isErrorLine) : kept;
  }, [log, level]);

  return (
    <Card
      size="small"
      title="Run log"
      extra={
        <Flex gap={8} align="center">
          <Segmented
            size="small"
            value={level}
            onChange={setLevel}
            options={[
              { label: "All", value: "all" },
              { label: "Errors", value: "errors" },
            ]}
          />
          <Button size="small" icon={<ReloadOutlined />} onClick={load} loading={loading}>Refresh</Button>
        </Flex>
      }
    >
      {error ? <Alert type="error" title={error} showIcon /> : null}
      {log === null ? (
        <Empty description="Load the log to inspect runner output." />
      ) : lines.length === 0 ? (
        <Typography.Text type="secondary">
          {level === "errors" ? "No error lines in this log." : "The log is empty."}
        </Typography.Text>
      ) : (
        <div
          className="benchmark-run-log"
          style={{
            maxHeight: 320,
            overflowY: "auto",
            background: "var(--bitcraft-color-fill-tertiary, #fafafa)",
            border: "1px solid var(--bitcraft-color-border-secondary, #f0f0f0)",
            borderRadius: 6,
            padding: 12,
          }}
        >
          <Typography.Text code style={{ whiteSpace: "pre-wrap", wordBreak: "break-word", display: "block" }}>
            {lines.join("\n")}
          </Typography.Text>
        </div>
      )}
    </Card>
  );
}
