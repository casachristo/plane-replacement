import type { NextConfig } from 'next';

const apiBase = process.env.WAYPOINT_API_BASE ?? 'http://localhost:8080';

const config: NextConfig = {
  output: 'standalone',
  async rewrites() {
    return [
      { source: '/api/v1/:path*', destination: `${apiBase}/api/v1/:path*` },
      { source: '/auth/:path*', destination: `${apiBase}/auth/:path*` },
    ];
  },
};

export default config;
