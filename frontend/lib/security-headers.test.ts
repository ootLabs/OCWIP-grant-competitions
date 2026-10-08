import { describe, expect, it } from "vitest";

import { contentSecurityPolicy, originOf } from "./security-headers";

describe("contentSecurityPolicy", () => {
  it("lets in the site, the nonce and the API, and nothing else", () => {
    const policy = contentSecurityPolicy({ nonce: "abc", apiOrigin: "https://api.example.pl", development: false });

    expect(policy).toContain("script-src 'self' 'nonce-abc' 'strict-dynamic'");
    expect(policy).not.toContain("unsafe-eval");
    expect(policy).toContain("connect-src 'self' https://api.example.pl");
    expect(policy).toContain("frame-ancestors 'none'");
    expect(policy).toContain("frame-src https://challenges.cloudflare.com;");
    expect(policy).toContain("object-src 'none'");
  });

  it("allows evaluation and the refresh socket only in development", () => {
    const policy = contentSecurityPolicy({ nonce: "abc", apiOrigin: null, development: true });

    expect(policy).toContain("'unsafe-eval'");
    expect(policy).toContain("connect-src 'self' ws:");
  });
});

describe("originOf", () => {
  it("keeps only the origin of the API address", () => {
    expect(originOf("https://konkursy.example.pl/api")).toBe("https://konkursy.example.pl");
    expect(originOf(undefined)).toBeNull();
    expect(originOf("/api")).toBeNull();
  });
});
