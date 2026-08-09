import { ApiError, getApiErrorDescription } from "../api/client.js";

const messagesByCode = {
  "TutorRuntime.NoActiveRevision": "This tutor is not ready for chat yet. Activate a configured revision first.",
  "TutorRuntime.PromptNotConfigured": "This tutor does not have a prompt configured.",
  "TutorRuntime.InteractionModelNotConfigured": "This tutor does not have an interaction model configured.",
  "TutorRuntime.InteractionModelDisabled": "This tutor's interaction model is currently unavailable.",
  "TutorRuntime.EmbeddingModelDisabled": "Tutor knowledge could not be searched because its embedding model is unavailable.",
  "TutorRuntime.EmbeddingUnavailable": "Tutor knowledge could not be searched because its embedding model is unavailable.",
  "TutorRuntime.SnapshotMissing": "This tutor needs to be activated again before it can be used.",
  "TutorRuntime.KnowledgeConfigurationInvalid": "This tutor's knowledge is not ready. Vectorize its documents and activate a new revision.",
  "TutorRuntime.TopKInvalid": "Retrieval depth must be between 1 and 10.",
  "TutorRuntime.HistoryTooLarge": "This conversation is too long to send. Start a new chat and try again.",
  "Ollama.Unavailable": "The configured Ollama runtime is unavailable. Please try again after the AI service is running.",
  "Ollama.ModelNotFound": "The tutor's configured model is not installed in Ollama.",
};

export function getTutorRuntimeError(error) {
  if (error instanceof ApiError && error.body && typeof error.body === "object") {
    if (messagesByCode[error.body.code]) return messagesByCode[error.body.code];
  }
  return getApiErrorDescription(error, "The tutor could not answer right now. Please try again.");
}

export function buildBoundedHistory(messages) {
  const history = messages
    .filter((message) => !message.local && !message.error && ["user", "assistant"].includes(message.role))
    .slice(-20)
    .map((message) => ({ role: message.role, content: message.content }));

  let total = history.reduce((sum, message) => sum + message.content.length, 0);
  while (history.length && total > 30000) {
    total -= history[0].content.length;
    history.shift();
  }
  return history;
}

export const sourcePercent = (score) => `${Math.round(Math.max(0, Math.min(1, score)) * 100)}%`;
