export const API_PATH = "/api/admin/runtime-engines";

export const PROVIDER_TYPES = [
  { value: 0, label: "Ollama", defaultEndpoint: "http://localhost:11434" },
  { value: 1, label: "OpenAI", defaultEndpoint: "https://api.openai.com/v1" },
  {
    value: 101,
    label: "Anthropic",
    defaultEndpoint: "https://api.anthropic.com",
  },
];

export const PROVIDER_LABELS = Object.fromEntries(
  PROVIDER_TYPES.map((provider) => [provider.value, provider.label]),
);

/**
 * Case-insensitive search across an engine's name/endpoint/provider label.
 */
export function filterEnginesBySearch(engines, query) {
  const trimmed = query.trim().toLowerCase();
  if (!trimmed) return engines;
  return engines.filter((engine) =>
    [engine.name, engine.endpoint, PROVIDER_LABELS[engine.providerType]].some(
      (value) => value?.toLowerCase().includes(trimmed),
    ),
  );
}

/**
 * Decides the endpoint value to show after the provider type changes.
 * Only auto-fills the new provider's default endpoint when the field is
 * empty or still holds another provider's default (i.e. the user hasn't
 * typed a custom endpoint); otherwise leaves their input untouched.
 */
export function resolveEndpointForProviderChange(
  providerType,
  currentEndpoint,
) {
  const matched = PROVIDER_TYPES.find((p) => p.value === providerType);
  if (!matched) return currentEndpoint;
  const shouldAutofill =
    !currentEndpoint ||
    PROVIDER_TYPES.some((p) => p.defaultEndpoint === currentEndpoint);
  return shouldAutofill ? matched.defaultEndpoint : currentEndpoint;
}
