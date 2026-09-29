import { describe, expect, it } from "vitest";
import { NextRequest } from "next/server";

import { middleware } from "./middleware";

describe("middleware", () => {
  it("puts the security headers and a fresh nonce on every page", () => {
    const first = middleware(new NextRequest("http://localhost:3000/competitions"));
    const second = middleware(new NextRequest("http://localhost:3000/competitions"));

    const policy = first.headers.get("Content-Security-Policy") ?? "";
    expect(policy).toMatch(/script-src 'self' 'nonce-[^']+'/);
    expect(policy).not.toBe(second.headers.get("Content-Security-Policy"));
    expect(first.headers.get("Permissions-Policy")).toContain("camera=()");
    // A withdrawn feature name makes the browser log an error on every page.
    expect(first.headers.get("Permissions-Policy")).not.toContain("interest-cohort");
    expect(first.headers.get("X-Content-Type-Options")).toBe("nosniff");
    expect(first.headers.get("Referrer-Policy")).toBe("no-referrer");
  });
});
