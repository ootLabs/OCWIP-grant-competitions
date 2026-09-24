import { StrictMode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { VerifyEmail } from "./verify-email";

function respondWith(status: number, body?: unknown) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { "content-type": "application/problem+json" },
    }),
  );
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const deadLink =
  "Nie udało się potwierdzić adresu e-mail. Link może być nieprawidłowy, wygasły, lub konto zostało już potwierdzone.";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("VerifyEmail", () => {
  it("confirms once, even when React runs the effect twice", async () => {
    const fetchMock = respondWith(200);

    render(
      <StrictMode>
        <VerifyEmail returnUrl={null} token="t1" userId="u1" />
      </StrictMode>,
    );

    expect((await screen.findByText(/jest potwierdzony/)).textContent).toContain(
      "Możesz się teraz zalogować",
    );
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
      userId: "u1",
      token: "t1",
    });
  });

  it("leads to signing in with the way back from the mail", async () => {
    respondWith(200);

    render(<VerifyEmail returnUrl="/competitions/abc" token="t1" userId="u1" />);

    expect(
      (await screen.findByRole("link", { name: "Zaloguj się" })).getAttribute(
        "href",
      ),
    ).toBe("/login?returnUrl=%2Fcompetitions%2Fabc");
  });

  it("shows the backend's sentence for a dead link, a way to sign in and a new link", async () => {
    respondWith(400, { detail: deadLink });

    render(<VerifyEmail returnUrl={null} token="t1" userId="u1" />);

    expect((await screen.findByRole("alert")).textContent).toBe(deadLink);
    expect(screen.getByRole("link", { name: "Zaloguj się" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Wyślij nowy link" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Spróbuj ponownie" })).toBeNull();
  });

  it("opened without a link, asks for a new one and shows no error", () => {
    const fetchMock = respondWith(200);

    render(<VerifyEmail returnUrl="/competitions/abc" token={null} userId={null} />);

    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.getByRole("button", { name: "Wyślij nowy link" })).toBeTruthy();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("does not call the backend for a link without its token", () => {
    const fetchMock = respondWith(200);

    render(<VerifyEmail returnUrl={null} token={null} userId="u1" />);

    expect(screen.getByRole("alert").textContent).toContain("Link jest niepełny");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("offers a retry with the same link when the server did not answer", async () => {
    const fetchMock = vi
      .fn()
      .mockRejectedValueOnce(new TypeError("Failed to fetch"))
      .mockResolvedValueOnce(new Response(null, { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    render(<VerifyEmail returnUrl={null} token="t1" userId="u1" />);

    expect((await screen.findByRole("alert")).textContent).toBe(
      "Nie udało się teraz połączyć z serwerem. Spróbuj ponownie za chwilę.",
    );
    fireEvent.click(screen.getByRole("button", { name: "Spróbuj ponownie" }));

    expect(await screen.findByText(/jest potwierdzony/)).toBeTruthy();
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("asks for a new link with the way back and answers the same for any address", async () => {
    respondWith(400, { detail: deadLink });

    render(<VerifyEmail returnUrl="/competitions/abc" token="t1" userId="u1" />);
    await screen.findByRole("alert");

    const resendMock = respondWith(200);
    fireEvent.change(screen.getByLabelText("Adres e-mail"), {
      target: { value: "nieznany@example.org" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Wyślij nowy link" }));

    const status = await screen.findByRole("status");
    expect(status.textContent).toContain("Jeśli ten adres czeka na potwierdzenie");
    expect(status.textContent).not.toContain("nieznany@example.org");
    expect(JSON.parse(resendMock.mock.calls[0][1].body)).toEqual({
      email: "nieznany@example.org",
      returnUrl: "/competitions/abc",
    });
  });
});
