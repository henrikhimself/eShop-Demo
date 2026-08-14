import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { bffFetch } from "./bff-fetch";

function clearCookies(): void {
  for (const entry of document.cookie.split(";")) {
    const name = entry.split("=")[0]?.trim();
    if (name) {
      document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`;
    }
  }
}

describe("bffFetch", () => {
  let fetchMock: ReturnType<typeof vi.fn>;
  let originalLocation: Location;

  beforeEach(() => {
    clearCookies();
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    originalLocation = window.location;
    // jsdom's window.location isn't a plain writable property, so it can't be
    // reassigned directly - redefine it instead.
    Object.defineProperty(window, "location", {
      configurable: true,
      value: { href: "" } as unknown as Location,
    });
  });

  afterEach(() => {
    Object.defineProperty(window, "location", {
      configurable: true,
      value: originalLocation,
    });
    vi.unstubAllGlobals();
    clearCookies();
  });

  it("fetches an antiforgery token once when the XSRF-TOKEN cookie is missing", async () => {
    fetchMock.mockImplementation(async (input: string) => {
      if (input === "/bff/api/antiforgery/token") {
        document.cookie = "XSRF-TOKEN=fetched-token; path=/";
        return new Response(null, { status: 204 });
      }
      return new Response(null, { status: 200 });
    });

    await bffFetch("/bff/api/drafts/movies", { method: "POST" });
    await bffFetch("/bff/api/drafts/movies", { method: "POST" });

    const tokenCalls = fetchMock.mock.calls.filter(([input]) => input === "/bff/api/antiforgery/token");
    expect(tokenCalls).toHaveLength(1);
  });

  it("attaches the X-XSRF-TOKEN header on mutating calls", async () => {
    document.cookie = "XSRF-TOKEN=existing-token; path=/";
    fetchMock.mockResolvedValue(new Response(null, { status: 200 }));

    await bffFetch("/bff/api/drafts/movies/1", { method: "PUT", body: "{}" });

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    const headers = new Headers(init.headers);
    expect(headers.get("X-XSRF-TOKEN")).toBe("existing-token");
  });

  it("does not attach the header or fetch a token on GET calls", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 200 }));

    await bffFetch("/bff/api/drafts");

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit | undefined];
    const headers = new Headers(init?.headers);
    expect(headers.get("X-XSRF-TOKEN")).toBeNull();
  });

  it("redirects to /bff/login on a 401 response", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 401 }));

    await bffFetch("/bff/api/drafts");

    expect(window.location.href).toBe("/bff/login");
  });

  it("redirects to /bff/login when fetching an antiforgery token 401s", async () => {
    fetchMock.mockImplementation(async (input: string) => {
      if (input === "/bff/api/antiforgery/token") {
        return new Response(null, { status: 401 });
      }
      return new Response(null, { status: 200 });
    });

    await bffFetch("/bff/api/drafts/movies", { method: "POST" }).catch(() => {
      // Expected: ensureXsrfToken throws after redirecting, same as a top-level 401.
    });

    expect(window.location.href).toBe("/bff/login");
  });

  it("refreshes a stale XSRF-TOKEN cookie and retries once on an antiforgery-invalid response", async () => {
    document.cookie = "XSRF-TOKEN=stale-token; path=/";
    let mutatingCallCount = 0;

    fetchMock.mockImplementation(async (input: string, init?: RequestInit) => {
      if (input === "/bff/api/antiforgery/token") {
        document.cookie = "XSRF-TOKEN=fresh-token; path=/";
        return new Response(null, { status: 204 });
      }

      mutatingCallCount += 1;
      const headers = new Headers(init?.headers);
      if (headers.get("X-XSRF-TOKEN") === "stale-token") {
        return new Response(null, { status: 400, headers: { "X-Antiforgery-Invalid": "true" } });
      }

      return new Response(null, { status: 200 });
    });

    const response = await bffFetch("/bff/api/drafts/movies/1", { method: "PUT", body: "{}" });

    expect(response.status).toBe(200);
    expect(mutatingCallCount).toBe(2);
    expect(readCookieForTest("XSRF-TOKEN")).toBe("fresh-token");
  });

  it("does not retry a 400 that isn't flagged as an antiforgery failure", async () => {
    document.cookie = "XSRF-TOKEN=existing-token; path=/";
    fetchMock.mockResolvedValue(new Response(null, { status: 400 }));

    const response = await bffFetch("/bff/api/drafts/movies/1/images", { method: "POST" });

    expect(response.status).toBe(400);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});

function readCookieForTest(name: string): string | undefined {
  return document.cookie
    .split("; ")
    .find((entry) => entry.startsWith(`${name}=`))
    ?.slice(name.length + 1);
}
