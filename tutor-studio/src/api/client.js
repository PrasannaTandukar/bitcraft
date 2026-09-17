// Centralized API client for Bitcraft Tutor Studio (D40).
//
// Design notes:
//   - Plain JavaScript per D29 (TypeScript deferred). The signature uses a single
//     `options` object so a future TS migration only needs to add types, not
//     restructure call sites.
//   - All callers should pass a path (e.g. "/api/documents"); the helper
//     prefixes API_BASE_URL.
//   - Authorization header is auto-attached from localStorage when present.
//   - 401 triggers session cleanup because authentication is invalid. A 403 is
//     an authorization denial and must preserve the valid session.
//   - The Redux store is imported lazily inside `clearAuthSession` to avoid a
//     circular import between this module and store/authSlice.

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:8000";

const TOKEN_KEY = "token";
const USER_KEY = "user";
const ROLES_KEY = "roles";

async function clearAuthSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
  localStorage.removeItem(ROLES_KEY);

  try {
    const [{ default: store }, { logout }] = await Promise.all([
      import("../store/index.js"),
      import("../store/authSlice.js"),
    ]);
    store.dispatch(logout());
  } catch {
    // Store may not yet be initialized (e.g. early application startup or tests).
    // localStorage cleanup above is still sufficient to invalidate the session.
  }
}

export class ApiError extends Error {
  constructor(status, statusText, body) {
    super(`HTTP ${status}: ${statusText || ""}`.trim());
    this.name = "ApiError";
    this.status = status;
    this.statusText = statusText;
    this.body = body;
  }
}

function parseErrorBody(body) {
  if (typeof body !== "string") return body;
  try {
    return JSON.parse(body);
  } catch {
    return body;
  }
}

function isSafeErrorText(value) {
  if (typeof value !== "string") return false;
  const text = value.trim();
  if (!text || text.length > 500) return false;
  return !/(stack\s*trace|exception|\s+at\s+\S+\(|\/src\/|\\src\\)/i.test(text);
}

export function getApiErrorDescription(error, fallback = "The request could not be completed.") {
  if (!(error instanceof ApiError)) {
    return fallback;
  }

  const body = parseErrorBody(error.body);
  if (typeof body === "string" && isSafeErrorText(body)) {
    return body.trim();
  }

  if (body && typeof body === "object") {
    // ProblemDetails errors dictionary: e.g. { errors: { "Cases[0].InputText": ["Input text is required."] } }
    if (body.errors && typeof body.errors === "object") {
      const errorValues = Object.values(body.errors).flat().filter(isSafeErrorText);
      if (errorValues.length > 0) {
        return errorValues.join(" ");
      }
    }
    for (const key of ["description", "title", "message"]) {
      if (isSafeErrorText(body[key])) return body[key].trim();
    }
  }

  if (error.status === 403) return "You do not have permission to perform this action.";
  if (error.status === 404) return "The requested item could not be found.";
  return fallback;
}

/**
 * Fetch wrapper for the Bitcraft API.
 *
 * @param {string} path Path beginning with "/" (e.g. "/api/documents").
 * @param {object} [options]
 * @param {string} [options.method] Defaults to "GET" (or "POST" when `json` is provided).
 * @param {Record<string,string>} [options.headers] Additional headers; merged with defaults.
 * @param {*} [options.json] Object to JSON-stringify as the request body. Sets Content-Type.
 * @param {BodyInit} [options.body] Raw body (use `json` for objects).
 * @param {AbortSignal} [options.signal]
 * @param {boolean} [options.auth=true] When false, no Bearer header is attached and
 *                                       401 does NOT trigger logout (for login/register).
 * @param {"json"|"text"|"blob"|"none"} [options.parse="json"] Response parse strategy.
 * @returns {Promise<*>} Parsed body, or null for 204/empty.
 * @throws {ApiError} On non-2xx responses.
 */
export async function apiFetch(path, options = {}) {
  const {
    method,
    headers,
    json,
    body,
    signal,
    auth = true,
    parse = "json",
    ...rest
  } = options;

  const finalHeaders = { ...(headers || {}) };
  let finalBody = body;

  if (json !== undefined) {
    if (!finalHeaders["Content-Type"]) {
      finalHeaders["Content-Type"] = "application/json";
    }
    finalBody = JSON.stringify(json);
  }

  if (auth) {
    const token = localStorage.getItem(TOKEN_KEY);
    if (token && !finalHeaders["Authorization"]) {
      finalHeaders["Authorization"] = `Bearer ${token}`;
    }
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...rest,
    method: method || (finalBody !== undefined ? "POST" : "GET"),
    headers: finalHeaders,
    body: finalBody,
    signal,
  });

  if (auth && response.status === 401) {
    // Fire-and-forget: do not await, so the throw below is not delayed.
    void clearAuthSession();
  }

  if (!response.ok) {
    let errorBody = null;
    try {
      const errorText = await response.text();
      errorBody = parseErrorBody(errorText);
    } catch {
      // ignore body read failure
    }
    throw new ApiError(response.status, response.statusText, errorBody);
  }

  if (parse === "none" || response.status === 204) {
    return null;
  }
  if (parse === "blob") return response.blob();
  if (parse === "text") {
    return response.text();
  }

  const text = await response.text();
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}
