import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { ForgotPasswordForm } from "./forgot-password-form";

function submit(email: string) {
  fireEvent.change(screen.getByLabelText("Adres e-mail"), {
    target: { value: email },
  });
  fireEvent.click(screen.getByRole("button", { name: "Wyślij link" }));
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ForgotPasswordForm", () => {
  it("answers the same for any address and does not repeat it", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(null, { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    render(<ForgotPasswordForm />);
    submit("nieznany@example.org");

    const status = await screen.findByRole("status");
    expect(status.textContent).toContain("Jeśli istnieje konto z tym adresem");
    expect(status.textContent).not.toContain("nieznany@example.org");
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
      email: "nieznany@example.org",
    });
    expect(
      screen.getByRole("link", { name: "Wróć do logowania" }).getAttribute("href"),
    ).toBe("/login");
  });

  it("passes on the rate limit sentence", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({ detail: "Zbyt wiele prób z tego adresu." }),
          {
            status: 429,
            headers: { "content-type": "application/problem+json" },
          },
        ),
      ),
    );

    render(<ForgotPasswordForm />);
    submit("a@example.org");

    expect((await screen.findByRole("alert")).textContent).toBe(
      "Zbyt wiele prób z tego adresu.",
    );
  });

  it("says nothing technical when the server is down", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ detail: "Npgsql timeout" }), {
          status: 503,
          headers: { "content-type": "application/problem+json" },
        }),
      ),
    );

    render(<ForgotPasswordForm />);
    submit("a@example.org");

    expect((await screen.findByRole("alert")).textContent).toBe(
      "Nie udało się teraz połączyć z serwerem. Spróbuj ponownie za chwilę.",
    );
  });
});
