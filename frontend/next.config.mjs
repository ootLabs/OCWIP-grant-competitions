/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // The container filesystem is a bind mount, where inotify events are lost.
  webpack: (config) => {
    config.watchOptions = { poll: 1000, aggregateTimeout: 300 };
    return config;
  },
};

export default nextConfig;
