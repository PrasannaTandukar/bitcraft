export const METRIC_DETAILS = {
  faithfulness: {
    name: "Faithfulness",
    description: "Estimates whether answer claims are supported by the supplied context.",
  },
  answer_relevancy: {
    name: "Answer relevancy",
    description: "Estimates how directly the answer addresses the question.",
  },
  hallucination: {
    name: "Groundedness / hallucination",
    description: "Estimates whether claims are grounded by context or an expected answer.",
  },
  correctness: {
    name: "Expected-answer correctness",
    description: "Compares the generated answer with the optional expected answer.",
  },
};

export const emptyBenchmarkCase = () => ({
  inputText: "",
  expectedOutput: "",
  contextText: "",
  metadataJson: "",
  actualOutput: "",
});

export const safeParseJson = (value, fallback = null) => {
  if (value === null || value === undefined || value === "") return fallback;
  if (typeof value === "object") return value;
  try {
    return JSON.parse(value);
  } catch {
    return fallback;
  }
};

export const parseMetrics = (metricsJson) => {
  const parsed = safeParseJson(metricsJson, []);
  const metrics = Array.isArray(parsed) ? parsed : parsed?.metrics;
  if (!Array.isArray(metrics)) return [];
  return metrics.filter((metric) => metric && typeof metric.key === "string" && metric.enabled !== false);
};

export const metricName = (key) => METRIC_DETAILS[key]?.name ?? key?.replaceAll("_", " ") ?? "Unknown metric";

export const formatPercent = (value) => {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? `${(numeric * 100).toFixed(1)}%` : "—";
};

export const formatProgressPercent = (value) => {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? `${Math.min(100, Math.max(0, numeric)).toFixed(0)}%` : "0%";
};

export const formatNumber = (value, digits = 1) => {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric.toFixed(digits) : "—";
};

export const formatDateTime = (value) => {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleString();
};

export const revisionStatus = (value) => {
  if (typeof value === "string") return value;
  return ["Draft", "Published", "Active", "Archived"][value] ?? "Unknown";
};

export const isUsableRevision = (revision) => {
  const status = revisionStatus(revision?.status);
  return (status === "Published" || status === "Active") &&
    Boolean(revision?.promptVersionId && revision?.interactionModelId) &&
    revision?.interactionModelEnabled !== false &&
    revision?.runtimeEngineEnabled !== false &&
    Boolean(revision?.latestSnapshotId);
};

export const runStatusLabel = (run) => {
  if (run?.status === "Running" && run?.progressStage === "Generating") return "Generating";
  if (run?.status === "Running" && run?.progressStage === "Evaluating") return "Evaluating";
  return run?.status ?? "Unknown";
};

export const scorerDisplayName = (engineName) =>
  engineName === "deepeval-deterministic" ? "Deterministic local scorer" : engineName || "Pending";

export const summarizeMetrics = (cases) => {
  const totals = new Map();
  for (const runCase of cases ?? []) {
    for (const metric of parseMetrics(runCase.metricsJson)) {
      const score = Number(metric.score);
      const threshold = Number(metric.threshold);
      if (!Number.isFinite(score)) continue;
      const current = totals.get(metric.key) ?? { key: metric.key, total: 0, count: 0, threshold: Number.isFinite(threshold) ? threshold : null };
      current.total += score;
      current.count += 1;
      if (Number.isFinite(threshold)) current.threshold = threshold;
      totals.set(metric.key, current);
    }
  }
  return [...totals.values()].map((metric) => ({
    key: metric.key,
    average: metric.count ? metric.total / metric.count : null,
    threshold: metric.threshold,
    passed: Number.isFinite(metric.threshold) && metric.count > 0
      ? metric.total / metric.count >= metric.threshold
      : null,
  }));
};
