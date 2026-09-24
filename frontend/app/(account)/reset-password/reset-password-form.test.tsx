import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { ResetPasswordForm } from "./reset-password-form";

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

function submit(password = "Nowe123!") {
  fireEvent.change(screen.getByLabelText("Nowe hasło"), {
    target: { value: password },
  });
  fireEvent.click(screen.getByRole("button", { name: "Ustaw nowe hasło" }));
}

const deadLink = "Link do resetowania hasła jest nieprawidłowy lub wygasł. Poproś o nowy.";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ResetPasswordForm", () => {
  it("sets the password with the token from the mail and leads to signing in", async () => {
    const fetchMock = respondWith(200);

    render(<ResetPasswordForm token="t1" userId="u1" />);
    expect(
      screen.getByLabelText("Nowe hasło").getAttribute("autocomplete"),
    ).toBe("new-password");
    submit();

    expect((await screen.findByRole("status")).textContent).toContain(
      "Hasło zostało zmienione",
    );
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
      userId: "u1",
      token: "t1",
      newPassword: "Nowe123!",
    });
    expect(
      screen
        .getByRole("link", { name: "Zaloguj się nowym hasłem" })
        .getAttribute("href"),
    ).toBe("/login");
  });

  it("keeps the form and marks the field when the policy refused the password", async () => {
    respondWith(400, {
      errors: { newPassword: ["Hasło musi zawierać co najmniej jedną cyfrę."] },
    });

    render(<ResetPasswordForm token="t1" userId="u1" />);
    submit("slabe");

    expect((await screen.findByRole("alert")).textContent).toBe(
      "Popraw zaznaczone pola.",
    );
    const input = screen.getByLabelText("Nowe hasło") as HTMLInputElement;
    expect(input.getAttribute("aria-invalid")).toBe("true");
    expect(input.value).toBe("");
    expect(screen.getByText(/co najmniej jedną cyfrę/)).toBeTruthy();
  });

  it("replaces the form with the way to a new link when the link is dead", async () => {
    respondWith(400, { detail: deadLink });

    render(<ResetPasswordForm token="t1" userId="u1" />);
    submit();

    expect((await screen.findByRole("alert")).textContent).toBe(deadLink);
    expect(screen.queryByLabelText("Nowe hasło")).toBeNull();
    expect(
      screen.getByRole("link", { name: "Poproś o nowy link" }).getAttribute("href"),
    ).toBe("/forgot-password");
  });

  it("does not offer the form for a link without its token", () => {
    const fetchMock = respondWith(200);

    render(<ResetPasswordForm token={null} userId="u1" />);

    expect(screen.getByRole("alert").textContent).toContain("Link jest niepełny");
    expect(screen.queryByLabelText("Nowe hasło")).toBeNull();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("keeps the password and the form when the server is down", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new TypeError("Failed to fetch")),
    );

    render(<ResetPasswordForm token="t1" userId="u1" />);
    submit();

    expect((await screen.findByRole("alert")).textContent).toBe(
      "Nie udało się teraz połączyć z serwerem. Spróbuj ponownie za chwilę.",
    );
    expect((screen.getByLabelText("Nowe hasło") as HTMLInputElement).value).toBe(
      "Nowe123!",
    );
  });
});
