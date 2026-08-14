import { NextRequest } from "next/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { GET } from "./route";

describe("bff proxy route", () => {
  let fetchMock: ReturnType<typeof vi.fn>;
  let originalBffUrl: string | undefined;

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    originalBffUrl = process.env.BFF_URL;
    process.env.BFF_URL = "https://bff.internal:7190";
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    process.env.BFF_URL = originalBffUrl;
  });

  it("derives X-Forwarded-Host from the request's own Host header", async () => {
    const request = new NextRequest("http://portal.example/bff/api/drafts", {
      headers: { host: "portal.example" },
    });

    await GET(request, { params: Promise.resolve({ slug: ["api", "drafts"] }) });

    const [proxyRequest] = fetchMock.mock.calls[0] as [Request];
    expect(proxyRequest.headers.get("X-Forwarded-Host")).toBe("portal.example");
  });

  it("ignores a client-supplied x-forwarded-host header, using the real Host instead", async () => {
    const request = new NextRequest("http://portal.example/bff/api/drafts", {
      headers: { host: "portal.example", "x-forwarded-host": "attacker.example" },
    });

    await GET(request, { params: Promise.resolve({ slug: ["api", "drafts"] }) });

    const [proxyRequest] = fetchMock.mock.calls[0] as [Request];
    expect(proxyRequest.headers.get("X-Forwarded-Host")).toBe("portal.example");
  });
});
