import type { NextConfig } from "next";
import { describe, expect, it } from "vitest";

// @ts-expect-error The config stays plain JavaScript and allowJs is off, so it has no types.
import config from "./next.config.mjs";

const nextConfig = config as NextConfig;

describe("next.config", () => {
  it("does not name the framework in a response header", () => {
    expect(nextConfig.poweredByHeader).toBe(false);
  });
});
