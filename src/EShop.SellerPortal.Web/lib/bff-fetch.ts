const XSRF_COOKIE_NAME = "XSRF-TOKEN";
const XSRF_HEADER_NAME = "X-XSRF-TOKEN";
const XSRF_TOKEN_URL = "/bff/api/antiforgery/token";
const LOGIN_URL = "/bff/login";

// Must match AntiforgeryEndpointFilter.cs's InvalidHeaderName exactly. See
// doc/CHRONICLE.md — antiforgery 400-vs-403 distinction.
const ANTIFORGERY_INVALID_HEADER = "X-Antiforgery-Invalid";

function readCookie(name: string): string | undefined {
  return document.cookie
    .split("; ")
    .find((entry) => entry.startsWith(`${name}=`))
    ?.slice(name.length + 1);
}

function clearCookie(name: string): void {
  document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`;
}

// The BFF's antiforgery endpoint sets the XSRF-TOKEN cookie as a side effect - this
// just triggers that once, then reads the cookie the browser now has.
async function ensureXsrfToken(): Promise<string> {
  const existing = readCookie(XSRF_COOKIE_NAME);
  if (existing) {
    return existing;
  }

  const response = await fetch(XSRF_TOKEN_URL);
  if (response.status === 401) {
    // If the session is gone, such as a stale auth cookie after a BFF restart,
    // redirect to login rather than falling through to the generic "no token" error
    // below.
    window.location.href = LOGIN_URL;
    throw new Error("Redirecting to login: the session is no longer authenticated.");
  }

  const token = readCookie(XSRF_COOKIE_NAME);
  if (!token) {
    throw new Error("Failed to obtain an XSRF-TOKEN cookie from the BFF.");
  }

  return token;
}

const SAFE_METHODS = new Set(["GET", "HEAD"]);

// Shared fetch helper: mutating verbs get an X-XSRF-TOKEN header, a 401 sends the
// browser to the BFF login flow, and a stale antiforgery pairing refreshes the token
// and retries once instead of surfacing an unrecoverable error.
export async function bffFetch(input: string, init: RequestInit = {}, isRetry = false): Promise<Response> {
  const method = (init.method ?? "GET").toUpperCase();
  const headers = new Headers(init.headers);
  const isMutating = !SAFE_METHODS.has(method);

  if (isMutating) {
    headers.set(XSRF_HEADER_NAME, await ensureXsrfToken());
  }

  const response = await fetch(input, { ...init, headers });

  if (response.status === 401) {
    window.location.href = LOGIN_URL;
    return response;
  }

  if (isMutating && !isRetry && response.headers.get(ANTIFORGERY_INVALID_HEADER) === "true") {
    clearCookie(XSRF_COOKIE_NAME);
    return bffFetch(input, init, true);
  }

  return response;
}
