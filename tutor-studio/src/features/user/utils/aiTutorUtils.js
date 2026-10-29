import { ApiError, getApiErrorDescription } from "../../../api/client.js";

const messagesByCode = {
  "TutorRuntime.ImageInputNotSupported": "This tutor's current model does not support image input.",
  "ChatAttachment.NotFound": "An image is unavailable. Please attach it again or start a new chat if it was in earlier messages.",
  "ChatAttachment.Expired": "An image has expired. Please attach it again or start a new chat if it was in earlier messages.",
  "ChatAttachment.UnsupportedType": "Choose a JPEG, PNG, or WebP image.",
  "ChatAttachment.InvalidImage": "This image is empty, corrupted, animated, or too large to decode. Please choose another image.",
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
  "TutorRuntime.Timeout": "The tutor took too long to respond. Please try again.",
  "TutorRuntime.RateLimited": "The tutor provider is busy. Please wait briefly and try again.",
  "TutorRuntime.ContextTooLarge": "This conversation exceeds the model's context limit. Start a new chat and try again.",
  "TutorRuntime.ProviderUnavailable": "The configured tutor provider is unavailable. Please try again later.",
  "TutorRuntime.ModelConfigurationInvalid": "The tutor's model configuration needs administrator attention.",
  "TutorConversation.Conflict": "This conversation changed in another request. Reload it before trying again.",
  "Ollama.Unavailable":
    "The configured Ollama runtime is unavailable. Please try again after the AI service is running.",
  "Ollama.ModelNotFound":
    "The tutor's configured model is not installed in Ollama.",
};

export function getTutorRuntimeError(error) {
  if (
    error instanceof ApiError &&
    error.body &&
    typeof error.body === "object"
  ) {
    if (messagesByCode[error.body.code]) return messagesByCode[error.body.code];
  }
  return getApiErrorDescription(
    error,
    "The tutor could not answer right now. Please try again.",
  );
}
