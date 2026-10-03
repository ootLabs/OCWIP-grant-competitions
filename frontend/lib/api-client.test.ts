import { describe, expect, it, vi, afterEach } from "vitest";
import { ApiError, apiBaseUrl, apiErrorMessage, apiFetch } from "./api-client";

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

  it("leaves the Content-Type header unset for a FormData body, so the browser can write its own boundary", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 201 }));
    vi.stubGlobal("fetch", fetchMock);

    const body = new FormData();
    body.append("file", new Blob(["tresc"]), "plik.pdf");

    await apiFetch("/health", { method: "POST", body });

    const headers = fetchMock.mock.calls[0][1].headers as Record<string, string> | undefined;
    expect(headers?.["Content-Type"]).toBeUndefined();
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

describe("apiErrorMessage", () => {
  it("prefers the detail the backend wrote for a person", () => {
    const error = new ApiError(409, "Request failed.", {}, "Nabór został zamknięty.");

    expect(apiErrorMessage(error, "Nie udało się.")).toBe("Nabór został zamknięty.");
  });

  it("falls back to the field messages of a validation problem", () => {
    // A 400 from ASP.NET validation has no detail at all, so without this the
    // operator saw "Nie udało się przyznać dofinansowania." and never learnt
    // how much was left in the pool (B-GUI-13, znalezisko 10).
    const error = new ApiError(400, "Request failed.", {
      awardedGrant: ["W puli zostało 20 000,00 zł. Kwota nie może być większa."],
    });

    expect(apiErrorMessage(error, "Nie udało się przyznać dofinansowania.")).toBe(
      "W puli zostało 20 000,00 zł. Kwota nie może być większa.",
    );
  });

  it("joins the messages when several fields are wrong", () => {
    const error = new ApiError(400, "Request failed.", {
      sections: ["Wskaż co najmniej jedną sekcję."],
      dueAt: ["Podaj termin poprawy."],
    });

    expect(apiErrorMessage(error, "Nie udało się zwrócić wniosku.")).toBe(
      "Wskaż co najmniej jedną sekcję. Podaj termin poprawy.",
    );
  });

  it("keeps the caller's fallback when the problem carries nothing readable", () => {
    const error = new ApiError(500, "Request failed.", { form: ["", "   "] });

    expect(apiErrorMessage(error, "Nie udało się zapisać.")).toBe("Nie udało się zapisać.");
  });

  it("keeps the caller's fallback for anything that is not an ApiError", () => {
    expect(apiErrorMessage(new TypeError("offline"), "Nie udało się zapisać.")).toBe(
      "Nie udało się zapisać.",
    );
  });
});
