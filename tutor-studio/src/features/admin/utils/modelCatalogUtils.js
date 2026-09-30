export const ENGINES_PATH = "/api/admin/runtime-engines";

// Embedding dimension reference heuristics for popular embedding models
export const EMBEDDING_DIM_MAP = {
  "nomic-embed-text": 768,
  "bge-m3": 1024,
  "bge-small": 384,
  "bge-large": 1024,
  "mxbai-embed-large": 1024,
  "all-minilm": 384,
  "all-mpnet-base-v2": 768,
  "text-embedding-3-small": 1536,
  "text-embedding-3-large": 3072,
  "text-embedding-ada-002": 1536,
  "embed-english-v3.0": 1024,
  "embed-multilingual-v3.0": 1024,
};

// Priority keywords / popular model tokens to rank top popular models first
export const POPULAR_EMBEDDING_PRIORITY = [
  "text-embedding-3-small",
  "text-embedding-3-large",
  "text-embedding-ada-002",
  "nomic-embed-text",
  "bge-m3",
  "bge-large",
  "bge-small",
  "mxbai-embed-large",
  "all-minilm",
  "all-mpnet-base-v2",
];

export const POPULAR_INTERACTION_PRIORITY = [
  "gpt-4o",
  "gpt-4o-mini",
  "gpt-4-turbo",
  "gpt-4",
  "gpt-3.5-turbo",
  "claude-3-5-sonnet",
  "claude-3-5-haiku",
  "claude-3-opus",
  "llama3.3",
  "llama3.2",
  "llama3.1",
  "llama3",
  "qwen2.5",
  "deepseek-r1",
  "deepseek-v3",
  "mistral",
  "mixtral",
  "phi4",
  "phi3",
  "gemma2",
];

export const catalogConfig = {
  embedding: {
    apiPath: "/api/admin/embedding-models",
    title: "Model catalog",
    description:
      "Manage embedding models used for document vectorization and tutor knowledge retrieval.",
    singular: "embedding model",
    providerPlaceholder: "nomic-embed-text / text-embedding-3-small / bge-m3",
  },
  interaction: {
    apiPath: "/api/admin/interaction-models",
    title: "Interaction models",
    description:
      "Manage the language models that power active tutor conversations and benchmark generation.",
    singular: "interaction model",
    providerPlaceholder: "llama3.3:70b / gpt-4o / claude-3-5-sonnet-20241022",
  },
};

export function scoreModelPopularity(modelName, isEmbedding) {
  const lower = modelName.toLowerCase();
  const priorityList = isEmbedding
    ? POPULAR_EMBEDDING_PRIORITY
    : POPULAR_INTERACTION_PRIORITY;
  const matchIndex = priorityList.findIndex((p) => lower.includes(p));
  if (matchIndex !== -1) {
    return matchIndex;
  }
  return 1000;
}

export function filterAndSortAvailableModels(
  rawModels,
  isEmbedding,
  limit = 10,
) {
  if (!rawModels || !rawModels.length) return [];

  const unique = Array.from(
    new Set(rawModels.map((m) => m?.trim()).filter(Boolean)),
  );

  const sorted = [...unique].sort((a, b) => {
    const scoreA = scoreModelPopularity(a, isEmbedding);
    const scoreB = scoreModelPopularity(b, isEmbedding);
    if (scoreA !== scoreB) {
      return scoreA - scoreB;
    }
    return a.localeCompare(b);
  });

  return sorted.slice(0, limit);
}

export function matchEmbeddingDimension(modelName) {
  if (EMBEDDING_DIM_MAP[modelName]) return EMBEDDING_DIM_MAP[modelName];
  const lower = modelName.toLowerCase();
  const found = Object.entries(EMBEDDING_DIM_MAP).find(([key]) =>
    lower.includes(key),
  );
  return found?.[1];
}

export function filterModelsBySearch(models, query) {
  const trimmed = query.trim().toLowerCase();
  if (!trimmed) return models;
  return models.filter((model) =>
    [model.name, model.providerModelName, model.runtimeEngineName].some(
      (value) => value?.toLowerCase().includes(trimmed),
    ),
  );
}
