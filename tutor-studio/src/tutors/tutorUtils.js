export const enumLabel = (value, labels, fallback = "Unknown") => {
  if (typeof value === "string") return value;
  return labels[value] ?? fallback;
};

export const tutorStatus = (value) => enumLabel(value, ["Draft", "Active", "Inactive", "Disabled"]);
export const revisionStatus = (value) => enumLabel(value, ["Draft", "Published", "Active", "Archived"]);
export const deploymentTarget = (value) => enumLabel(value, ["Local", "Server"]);

export const formatDateTime = (value) => {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleString();
};

export const parseJsonObject = (value) => {
  if (!value) return {};
  const parsed = typeof value === "string" ? JSON.parse(value) : value;
  if (!parsed || Array.isArray(parsed) || typeof parsed !== "object") {
    throw new Error("Enter a JSON object.");
  }
  return parsed;
};

export const prettyJson = (value) => {
  if (!value) return "{}";
  try {
    return JSON.stringify(typeof value === "string" ? JSON.parse(value) : value, null, 2);
  } catch {
    return String(value);
  }
};

export const revisionIsPublishable = (revision) =>
  revisionStatus(revision?.status) === "Draft" &&
  Boolean(revision?.promptVersionId) &&
  Boolean(revision?.interactionModelId) &&
  revision?.interactionModelEnabled !== false &&
  revision?.runtimeEngineEnabled !== false;

export const revisionIsBenchmarkReady = (revision) =>
  ["Active", "Published"].includes(revisionStatus(revision?.status)) &&
  Boolean(revision?.promptVersionId) &&
  Boolean(revision?.interactionModelId) &&
  revision?.interactionModelEnabled !== false &&
  revision?.runtimeEngineEnabled !== false &&
  Boolean(revision?.latestSnapshotId);

export const revisionLabel = (revision) =>
  `Revision ${revision?.versionNumber ?? "—"} · ${revisionStatus(revision?.status)}`;
