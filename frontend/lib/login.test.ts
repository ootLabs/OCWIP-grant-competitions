import { afterEach, describe, expect, it, vi } from "vitest";

import { ApiError } from "./api-client";
import {
  invalidCredentialsMessage,
  isRefusal,
  login,
  loginFailureMessage,
  unavailableMessage,
  withReturnUrl,
} from "./login";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("login", () => {
  it("posts the credentials and the returnUrl untouched to /login", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ redirectPath: "/competitions/abc" }), {
        status: 200,
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const session = await login("a@example.org", "Haslo123!", "/competitions/abc");

    expect(session.redirectPath).toBe("/competitions/abc");
    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/login$/);
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({
      email: "a@example.org",
      password: "Haslo123!",
      returnUrl: "/competitions/abc",
    });
  });
});

describe("loginFailureMessage", () => {
  it("shows the backend sentence for 401, 403 and 429", () => {
    expect(loginFailureMessage(new ApiError(401, "x", {}, "Z backendu 401"))).toBe(
      "Z backendu 401",
    );
    expect(loginFailureMessage(new ApiError(403, "x", {}, "Z backendu 403"))).toBe(
      "Z backendu 403",
    );
    expect(loginFailureMessage(new ApiError(429, "x", {}, "Za 15 min"))).toBe(
      "Za 15 min",
    );
  });

  it("falls back to the one credentials message without a backend sentence", () => {
    expect(loginFailureMessage(new ApiError(401, "x"))).toBe(
      invalidCredentialsMessage,
    );
  });

  it("answers a malformed request like wrong credentials", () => {
    expect(loginFailureMessage(new ApiError(400, "x", {}, "Pole email"))).toBe(
      invalidCredentialsMessage,
    );
  });

  it("never calls an outage a wrong password", () => {
    expect(loginFailureMessage(new ApiError(503, "x", {}, "Wewnętrzny opis"))).toBe(
      unavailableMessage,
    );
    expect(loginFailureMessage(new TypeError("Failed to fetch"))).toBe(
      unavailableMessage,
    );
  });
});

describe("isRefusal", () => {
  it("is true only when the server answered no", () => {
    expect(isRefusal(new ApiError(401, "x"))).toBe(true);
    expect(isRefusal(new ApiError(429, "x"))).toBe(true);
    expect(isRefusal(new ApiError(500, "x"))).toBe(false);
    expect(isRefusal(new TypeError("Failed to fetch"))).toBe(false);
  });
});

describe("withReturnUrl", () => {
  it("keeps the way back, encoded", () => {
    expect(withReturnUrl("/register", "/competitions/a b")).toBe(
      "/register?returnUrl=%2Fcompetitions%2Fa+b",
    );
  });

  it("leaves the path alone without one", () => {
    expect(withReturnUrl("/register", null)).toBe("/register");
    expect(withReturnUrl("/register", "")).toBe("/register");
  });
});
