// Every description and derivation below is grounded in the sidecar
// implementation, not in vendor marketing copy. Source files:
// sidecar/app/tools/deepeval_benchmark.py (deterministic scorer) and
// sidecar/app/tools/deepeval_metrics.py (LLM-as-a-judge path).
export const METRIC_DETAILS = {
  // Tier 1: Deterministic & Heuristic metrics
  faithfulness: {
    name: "Faithfulness (Claim Overlap)",
    tier: 1,
    description: "Measures the share of answer claims that the retrieval context supports.",
    derivation: "Splits the answer into claims. Divides context-supported claims by total claims. Returns 0 when no retrieval context exists.",
  },
  answer_relevancy: {
    name: "Answer Relevancy",
    tier: 1,
    description: "Measures token overlap between the question and the answer.",
    derivation: "Compares prompt tokens with response tokens and returns the overlap ratio. No judge model is used.",
  },
  hallucination: {
    name: "Groundedness / Hallucination",
    tier: 1,
    description: "Measures the share of answer claims that context or the expected answer grounds.",
    derivation: "Divides claims supported by context plus expected answer by total claims. Returns 0 when both are missing.",
  },
  correctness: {
    name: "Expected-answer Correctness",
    tier: 1,
    description: "Measures token overlap between the answer and the expected answer.",
    derivation: "Compares the expected answer with the actual answer and returns the overlap ratio. Returns 0 when no expected answer exists.",
  },
  contextual_precision: {
    name: "Contextual Precision",
    tier: 1,
    description: "Measures the share of answer claims that retrieved context supports.",
    derivation: "Uses the same claim-overlap ratio as faithfulness. Returns 0 when no retrieval context exists.",
  },
  contextual_recall: {
    name: "Contextual Recall",
    tier: 1,
    description: "Measures the share of answer claims that retrieved context covers.",
    derivation: "Uses the same claim-overlap ratio as faithfulness. Returns 0 when no retrieval context exists.",
  },
  contextual_relevancy: {
    name: "Contextual Relevancy",
    tier: 1,
    description: "Measures the share of answer claims that retrieved context supports.",
    derivation: "Uses the same claim-overlap ratio as faithfulness. Returns 0 when no retrieval context exists.",
  },
  pii_leakage: {
    name: "PII & Secret Guard",
    tier: 1,
    description: "Detects emails, phone numbers, and API keys in the answer.",
    derivation: "Runs three regex scans for email, phone, and secret patterns. Returns 1 when none match and 0 when any match.",
  },
  toxicity: {
    name: "Toxicity Check",
    tier: 1,
    description: "Scans the answer for a fixed list of harmful keywords.",
    derivation: "Searches for the keywords hate, kill, harm, and toxic. Returns 0.5 on a match and 1 when none match.",
  },
  bias: {
    name: "Bias Check",
    tier: 1,
    description: "Scans the answer for the same fixed keyword list as toxicity.",
    derivation: "Shares the toxicity keyword scan. Returns 0.5 on a match and 1 when none match.",
  },

  // Tier 2: Qualitative & LLM-as-a-Judge / G-Eval
  g_eval: {
    name: "G-Eval (Custom Rubric)",
    tier: 2,
    description: "Scores the answer against a custom rubric with a judge model.",
    derivation: "Falls back to prompt and answer token overlap in the deterministic scorer. The judge path uses deepeval GEval.",
  },
  prompt_alignment: {
    name: "Prompt Alignment",
    tier: 2,
    description: "Measures how closely the answer follows the prompt.",
    derivation: "Compares prompt tokens with response tokens and returns the overlap ratio. No judge model is used.",
  },
  role_violation: {
    name: "Persona & Role Adherence",
    tier: 2,
    description: "Measures whether the answer stays within the assigned persona.",
    derivation: "Falls back to prompt and answer token overlap. The deterministic scorer does not parse persona rules.",
  },
};

// Standardized benchmark suites are served by the backend
// (GET /api/standard-benchmark-runs/suites) from the canonical
// StandardBenchmarkSuiteRegistry. Do not hardcode suite lists here.

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

export const metricTier = (key) => METRIC_DETAILS[key]?.tier ?? 1;

export const metricTierBadge = (key) => {
  const tier = metricTier(key);
  if (tier === 1) {
    return {
      label: "Deterministic",
      color: "blue",
      tooltip: "100% reproducible exact / claim-overlap calculation (No LLM subjectivity).",
      symbol: "⚡",
    };
  }
  return {
    label: "LLM Judge",
    color: "purple",
    tooltip: "Qualitative evaluation scored via LLM-as-a-judge / G-Eval rubric.",
    symbol: "⚖️",
  };
};

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

export const progressIndicator = (run) => {
  if (run?.status !== "Running" && run?.status !== "Queued") return null;
  // Tutor runs carry completedCaseCount/totalCaseCount; standard runs carry
  // completedTasks/totalTasks. Read whichever shape is present.
  const completed = Number(run?.completedTasks ?? run?.completedCaseCount);
  const total = Number(run?.totalTasks ?? run?.totalCaseCount);
  if (Number.isFinite(completed) && Number.isFinite(total) && total > 0) return `${completed}/${total}`;
  const percent = Number(run?.progressPercent);
  if (Number.isFinite(percent) && percent > 0) return formatProgressPercent(percent);
  if (typeof run?.progressStage === "string" && run.progressStage.trim()) return run.progressStage;
  return null;
};

export const runStatusLabel = (run) => {
  if (run?.status === "Running" && run?.progressStage === "Generating") return "Generating";
  if (run?.status === "Running" && run?.progressStage === "Evaluating") return "Evaluating";
  const indicator = progressIndicator(run);
  if (indicator) return `In Progress · ${indicator}`;
  return run?.status ?? "Unknown";
};

export const toHistoryRow = (run, kind) => ({
  id: run?.id,
  kind,
  status: run?.status,
  statusLabel: runStatusLabel(run),
  model: kind === "tutor" ? run?.modelAlias : run?.interactionModelName,
  target: kind === "tutor" ? run?.benchmarkTemplateName : run?.benchmarkKey,
  score: kind === "tutor" ? run?.averageScore : run?.overallScore,
  count: kind === "tutor" ? run?.totalCaseCount : run?.taskCount,
  queuedUtc: run?.queuedUtc,
  completedUtc: run?.completedUtc,
  failureDescription: run?.failureDescription,
  raw: run,
});

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
