import { type NextRequest } from "next/server";

// Next.js's own route-handler passthrough pattern, not proxy.ts (lightweight
// redirect/rewrite only). See doc/System landscape.md — Seller Portal reverse proxy.
async function proxy(
  request: NextRequest,
  { params }: { params: Promise<{ slug: string[] }> },
): Promise<Response> {
  const bffUrl = process.env.BFF_URL;

  if (!bffUrl) {
    return new Response("BFF_URL is not configured.", { status: 500 });
  }

  const { slug } = await params;
  const targetUrl = new URL(`/bff/${slug.join("/")}`, bffUrl);
  targetUrl.search = request.nextUrl.search;

  const headers = new Headers(request.headers);
  // Read the raw Host header directly: x-forwarded-host is client-settable (could spoof
  // the OIDC redirect_uri host), and request.nextUrl.host is Next.js's own normalized
  // bind address, not the real Host header the client sent.
  const forwardedHost = request.headers.get("host");
  if (forwardedHost) {
    headers.set("X-Forwarded-Host", forwardedHost);
  }
  const forwardedProto = request.nextUrl.protocol.replace(":", "");
  headers.set("X-Forwarded-Proto", forwardedProto);

  const hasBody = request.method !== "GET" && request.method !== "HEAD";

  const proxyRequest = new Request(targetUrl, {
    method: request.method,
    headers,
    body: hasBody ? request.body : undefined,
    // Required by Node's fetch when passing a streaming ReadableStream body.
    duplex: hasBody ? "half" : undefined,
    // Never follow redirects here: a 3xx from the BFF (e.g. to Keycloak) must reach
    // the real browser, not be followed by this server-side proxy.
    redirect: "manual",
  } as RequestInit);

  return fetch(proxyRequest);
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
export const HEAD = proxy;
export const OPTIONS = proxy;
