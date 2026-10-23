import type { NextConfig } from "next";

// The C# API listens on loopback only; the browser always talks to Next.js (one origin, no CORS).
const backendUrl = process.env.BACKEND_URL ?? "http://127.0.0.1:5000";

const nextConfig: NextConfig = {
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${backendUrl}/api/:path*` }];
  },
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
