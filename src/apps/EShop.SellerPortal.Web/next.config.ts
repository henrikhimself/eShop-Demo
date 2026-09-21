import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Required by Aspire's AddNextJsApp for publish-time Dockerfile generation.
  output: "standalone",
  // Keeps a containerized run from sharing .next with a native one. See
  // doc/MEMORY.md — NEXT_DIST_DIR.
  distDir: process.env.NEXT_DIST_DIR ?? ".next",
  // The reverse proxy (PLAN-2.md §3) is the only local-development browser entry
  // point now - the browser's real Origin/Referer is the public
  // seller.eshop.local:8443 host, not this dev server's own internal bind address.
  // Without this, Next.js dev mode's cross-origin protection blocks every request
  // (HMR and regular fetches alike) with "Blocked cross-origin request".
  allowedDevOrigins: ["seller.eshop.local"],
};

export default nextConfig;
