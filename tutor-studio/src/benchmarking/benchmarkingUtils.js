export const METRIC_DETAILS = {
  // Tier 1: Deterministic & Heuristic metrics
  faithfulness: {
    name: "Faithfulness (Claim Overlap)",
    tier: 1,
    description: "Estimates whether answer claims are supported by the supplied context.",
  },
  answer_relevancy: {
    name: "Answer Relevancy",
    tier: 1,
    description: "Estimates how directly the answer addresses the question.",
  },
  hallucination: {
    name: "Groundedness / Hallucination",
    tier: 1,
    description: "Estimates whether claims are grounded by context or an expected answer.",
  },
  correctness: {
    name: "Expected-answer Correctness",
    tier: 1,
    description: "Compares the generated answer with the optional expected answer.",
  },
  contextual_precision: {
    name: "Contextual Precision",
    tier: 1,
    description: "Evaluates context relevance and signal-to-noise ratio in retrieved knowledge.",
  },
  contextual_recall: {
    name: "Contextual Recall",
    tier: 1,
    description: "Evaluates whether retrieved chunks cover all necessary facts.",
  },
  contextual_relevancy: {
    name: "Contextual Relevancy",
    tier: 1,
    description: "Scores the relevancy of retrieved chunks directly against the prompt.",
  },
  pii_leakage: {
    name: "PII & Secret Guard",
    tier: 1,
    description: "Deterministic regex detection for emails, phone numbers, and credentials (COPPA/GDPR-K).",
  },
  toxicity: {
    name: "Toxicity Check",
    tier: 1,
    description: "Deterministic keyword heuristic for harmful content.",
  },
  bias: {
    name: "Bias Check",
    tier: 1,
    description: "Heuristic scan for stereotypical bias indicators.",
  },

  // Tier 2: Qualitative & LLM-as-a-Judge / G-Eval
  g_eval: {
    name: "G-Eval (Custom Rubric)",
    tier: 2,
    description: "Multi-criteria Chain-of-Thought evaluation with custom rubrics.",
  },
  prompt_alignment: {
    name: "Prompt Alignment",
    tier: 2,
    description: "Evaluates semantic alignment with prompt instructions.",
  },
  role_violation: {
    name: "Persona & Role Adherence",
    tier: 2,
    description: "Audits whether tutor stays within designated persona boundaries.",
  },
};

export const STANDARD_BENCHMARKS = [
  { key: "mmlu", name: "MMLU", description: "Massive Multitask Language Understanding (57 subjects: STEM, humanities, etc.)" },
  { key: "gsm8k", name: "GSM8K", description: "Grade School Math (8,500 multi-step math problems testing numerical reasoning)" },
  { key: "big_bench_hard", name: "BIG-Bench Hard", description: "23 challenging multi-step reasoning and algorithmic tasks" },
  { key: "hellaswag", name: "HellaSwag", description: "Adversarial commonsense reasoning and sentence completion" },
  { key: "drop", name: "DROP", description: "Discrete Reasoning Over Paragraphs (reading comprehension, dates, counts)" },
  { key: "truthful_qa", name: "TruthfulQA", description: "Benchmark testing mimicry of human falsehoods and hallucination resistance" },
];

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
