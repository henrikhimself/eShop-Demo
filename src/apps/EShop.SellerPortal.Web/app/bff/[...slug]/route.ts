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
  // The reverse proxy (forwardPublicOrigin: true, see AppHost.cs) always overwrites
  // X-Forwarded-Host/-Proto with the public seller.eshop.local:8443 origin before this
  // handler ever sees the request - a client can't override them (PLAN-2.md §5.1).
  // The raw Host header is *not* usable here instead: with forwardPublicOrigin: true
  // the proxy forwards this app's own internal Host, not the public one.
  const forwardedHost = request.headers.get("x-forwarded-host");
  const forwardedProto = request.headers.get("x-forwarded-proto");
  if (!forwardedHost || !forwardedProto) {
    return new Response("Missing X-Forwarded-Host/X-Forwarded-Proto - this app must be reached through the reverse proxy.", { status: 400 });
  }
  headers.set("X-Forwarded-Host", forwardedHost);
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
