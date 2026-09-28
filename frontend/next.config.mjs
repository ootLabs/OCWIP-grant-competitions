/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // A self-contained server for the production image (T-110): the build
  // copies only what `node server.js` needs, without the dev dependencies.
  output: "standalone",
  // The development container's filesystem is a bind mount, where inotify
  // events are lost. Only there: a production build watches nothing.
  webpack: (config, { dev }) => {
    if (dev) {
      config.watchOptions = { poll: 1000, aggregateTimeout: 300 };
    }
    return config;
  },
};

export default nextConfig;
