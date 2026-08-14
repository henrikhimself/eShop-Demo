import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Required by Aspire's AddNextJsApp for publish-time Dockerfile generation.
  output: "standalone",
  // Keeps a containerized run from sharing .next with a native one. See
  // doc/MEMORY.md — NEXT_DIST_DIR.
  distDir: process.env.NEXT_DIST_DIR ?? ".next",
};

export default nextConfig;
