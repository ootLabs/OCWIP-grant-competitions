import { describe, expect, it, vi, afterEach } from "vitest";
import { ApiError, apiBaseUrl, apiFetch } from "./api-client";

describe("apiFetch", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("falls back to localhost when no API URL is configured", () => {
    expect(apiBaseUrl).toMatch(/^https?:\/\//);
  });

  it("sends credentials so the session cookie travels cross origin", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ status: "ok" }), { status: 200 }),
    );
    vi.stubGlobal("fetch", fetchMock);

    await apiFetch("/health");

    expect(fetchMock.mock.calls[0][1]).toMatchObject({ credentials: "include" });
  });

  it("throws ApiError without leaking the response body", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response("Npgsql: password authentication failed", { status: 500 }),
      ),
    );

    await expect(apiFetch("/health/db")).rejects.toBeInstanceOf(ApiError);
    await expect(apiFetch("/health/db")).rejects.not.toThrowError(/password/);
  });

  it("keeps validation messages attached to their field", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            title: "One or more validation errors occurred.",
            status: 400,
            errors: { email: ["To nie jest poprawny adres e-mail."] },
          }),
          {
            status: 400,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );

    const error = await apiFetch("/register").catch((thrown) => thrown);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).fieldErrors).toEqual({
      email: ["To nie jest poprawny adres e-mail."],
    });
  });

  it("carries ProblemDetails.detail for a caller that opts in, without changing the generic message", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            title: "Conflict",
            status: 409,
            detail: "Ten konkurs jest oznaczony jako nieaktywny.",
          }),
          { status: 409, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );

    const error = (await apiFetch("/health/db").catch((thrown) => thrown)) as ApiError;

    expect(error.detail).toBe("Ten konkurs jest oznaczony jako nieaktywny.");
    expect(error.message).toMatch(/failed/);
  });

  it("leaves detail null when the backend sends none", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response("boom", { status: 500 })),
    );

    const error = (await apiFetch("/health/db").catch((thrown) => thrown)) as ApiError;

    expect(error.detail).toBeNull();
  });

  it("survives a response with no body at all", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(null, { status: 202 })),
    );

    await expect(apiFetch("/register")).resolves.toBeUndefined();
  });
});
