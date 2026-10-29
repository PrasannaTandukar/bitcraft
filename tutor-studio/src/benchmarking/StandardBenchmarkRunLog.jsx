import { ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Empty, Flex, Segmented, Switch, Typography } from "antd";
import { useCallback, useEffect, useMemo, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { getStandardBenchmarkRunLog } from "./benchmarkingApi.js";

const AUTO_REFRESH_INTERVAL_MS = 3_000;

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

export default function StandardBenchmarkRunLog({ runId, isActive = false }) {
  const [log, setLog] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [level, setLevel] = useState("all");
  const [autoRefresh, setAutoRefresh] = useState(false);

  const load = useCallback(async () => {
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
  }, [runId]);

  // Auto-refresh (G4): only while the run is active and the user opted in.
  // The interval stops on terminal states.
  useEffect(() => {
    if (!autoRefresh || !isActive || !runId) return undefined;
    const timer = setInterval(load, AUTO_REFRESH_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [autoRefresh, isActive, runId, load]);

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
          {isActive ? (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Auto <Switch size="small" checked={autoRefresh} onChange={setAutoRefresh} />
            </Typography.Text>
          ) : null}
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
