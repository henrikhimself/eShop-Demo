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

  it("forwards the reverse proxy's own X-Forwarded-Host/X-Forwarded-Proto headers", async () => {
    // The reverse proxy (forwardPublicOrigin: true) always overwrites these headers
    // itself before this handler ever runs - a raw Host header is this app's own
    // internal address once it's proxied, not the public origin (PLAN-2.md §5.1).
    const request = new NextRequest("http://internal.example/bff/api/drafts", {
      headers: {
        host: "internal.example",
        "x-forwarded-host": "seller.eshop.local:8443",
        "x-forwarded-proto": "https",
      },
    });

    await GET(request, { params: Promise.resolve({ slug: ["api", "drafts"] }) });

    const [proxyRequest] = fetchMock.mock.calls[0] as [Request];
    expect(proxyRequest.headers.get("X-Forwarded-Host")).toBe("seller.eshop.local:8443");
    expect(proxyRequest.headers.get("X-Forwarded-Proto")).toBe("https");
  });

  it("rejects a request missing X-Forwarded-Host/X-Forwarded-Proto instead of guessing the origin", async () => {
    const request = new NextRequest("http://internal.example/bff/api/drafts", {
      headers: { host: "internal.example" },
    });

    const response = await GET(request, { params: Promise.resolve({ slug: ["api", "drafts"] }) });

    expect(response.status).toBe(400);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
