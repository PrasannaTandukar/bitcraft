import { apiFetch } from "../api/client.js";

export const listTutors = (signal) => apiFetch("/api/tutors", { signal });

export const listTutorRevisions = (tutorId, signal) =>
  apiFetch(`/api/tutors/${tutorId}/revisions`, { signal });

export const listBenchmarkTemplates = (signal) =>
  apiFetch("/api/benchmark/templates", { signal });

export const createBenchmarkTemplate = (payload) =>
  apiFetch("/api/benchmark/templates", { method: "POST", json: payload });

export const deleteBenchmarkTemplate = (templateId) =>
  apiFetch(`/api/benchmark/templates/${templateId}`, { method: "DELETE", parse: "none" });

export const createBenchmarkRun = (tutorId, payload) =>
  apiFetch(`/api/tutors/${tutorId}/benchmark-runs`, { method: "POST", json: payload });

export const listBenchmarkRuns = (tutorId, signal) =>
  apiFetch(`/api/tutors/${tutorId}/benchmark-runs`, { signal });

export const getBenchmarkRun = (tutorId, runId, signal) =>
  apiFetch(`/api/tutors/${tutorId}/benchmark-runs/${runId}`, { signal });

export const getBenchmarkProgress = (tutorId, runId, signal) =>
  apiFetch(`/api/tutors/${tutorId}/benchmark-runs/${runId}/progress`, { signal });

// ----- Standardized (model-scoped) DeepEval benchmark runs -----

export const listStandardBenchmarkSuites = (signal) =>
  apiFetch("/api/standard-benchmark-runs/suites", { signal });

export const listStandardBenchmarkRuns = (interactionModelId, signal) =>
  apiFetch(`/api/standard-benchmark-runs${interactionModelId ? `?interactionModelId=${interactionModelId}` : ""}`, { signal });

export const getStandardBenchmarkRun = (runId, signal) =>
  apiFetch(`/api/standard-benchmark-runs/${runId}`, { signal });

export const getStandardBenchmarkRunProgress = (runId, signal) =>
  apiFetch(`/api/standard-benchmark-runs/${runId}/progress`, { signal });

export const createStandardBenchmarkRun = (payload) =>
  apiFetch("/api/standard-benchmark-runs", { method: "POST", json: payload });

export const listInteractionModelsForAdmin = (signal) =>
  apiFetch("/api/admin/interaction-models", { signal });
