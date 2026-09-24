import { afterEach, describe, expect, it, vi } from "vitest";

import {
  accountFailure,
  fixFieldsMessage,
  forgotPassword,
  register,
  resendVerification,
  resetPassword,
  unavailableMessage,
  verifyEmail,
} from "./account";
import { ApiError } from "./api-client";

afterEach(() => {
  vi.unstubAllGlobals();
});

function stubFetch(status = 200) {
  const fetchMock = vi
    .fn()
    .mockResolvedValue(new Response(null, { status }));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function sent(fetchMock: ReturnType<typeof stubFetch>) {
  const [url, init] = fetchMock.mock.calls[0];
  return { path: new URL(String(url)).pathname, body: JSON.parse(init.body) };
}

describe("account calls", () => {
  it("registers with the returnUrl untouched and accepts an empty 202", async () => {
    const fetchMock = stubFetch(202);

    await register({
      email: "a@example.org",
      password: "Haslo123!",
      firstName: "Ada",
      lastName: "Nowak",
      returnUrl: "/competitions/abc",
    });

    expect(sent(fetchMock)).toEqual({
      path: "/register",
      body: {
        email: "a@example.org",
        password: "Haslo123!",
        firstName: "Ada",
        lastName: "Nowak",
        returnUrl: "/competitions/abc",
      },
    });
  });

  it("posts each of the other screens to its own route", async () => {
    let fetchMock = stubFetch();
    await verifyEmail("u1", "t1");
    expect(sent(fetchMock)).toEqual({
      path: "/verify-email",
      body: { userId: "u1", token: "t1" },
    });

    fetchMock = stubFetch();
    await resendVerification("a@example.org", "/competitions/abc");
    expect(sent(fetchMock)).toEqual({
      path: "/resend-verification",
      body: { email: "a@example.org", returnUrl: "/competitions/abc" },
    });

    fetchMock = stubFetch();
    await forgotPassword("a@example.org");
    expect(sent(fetchMock)).toEqual({
      path: "/forgot-password",
      body: { email: "a@example.org" },
    });

    fetchMock = stubFetch();
    await resetPassword("u1", "t1", "Nowe123!");
    expect(sent(fetchMock)).toEqual({
      path: "/reset-password",
      body: { userId: "u1", token: "t1", newPassword: "Nowe123!" },
    });
  });
});

describe("accountFailure", () => {
  it("points at the fields when the backend named them", () => {
    const failure = accountFailure(
      new ApiError(400, "x", { password: ["Za krótkie."] }),
    );

    expect(failure).toEqual({
      message: fixFieldsMessage,
      fieldErrors: { password: ["Za krótkie."] },
      refused: true,
    });
  });

  it("shows the backend sentence for a dead link and for the rate limit", () => {
    expect(
      accountFailure(new ApiError(400, "x", {}, "Link wygasł.")).message,
    ).toBe("Link wygasł.");
    expect(
      accountFailure(new ApiError(429, "x", {}, "Zbyt wiele prób z tego adresu."))
        .message,
    ).toBe("Zbyt wiele prób z tego adresu.");
  });

  it("never calls an outage the applicant's mistake", () => {
    for (const error of [
      new ApiError(503, "x", {}, "Wewnętrzny opis"),
      new ApiError(500, "x"),
      new TypeError("Failed to fetch"),
    ]) {
      expect(accountFailure(error)).toEqual({
        message: unavailableMessage,
        fieldErrors: {},
        refused: false,
      });
    }
  });
});
