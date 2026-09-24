import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { RegisterForm } from "./register-form";

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

function fillAndSubmit(email = "biuro@example.org") {
  fireEvent.change(screen.getByLabelText("Imię"), { target: { value: "Ada" } });
  fireEvent.change(screen.getByLabelText("Nazwisko"), {
    target: { value: "Nowak" },
  });
  fireEvent.change(screen.getByLabelText("Adres e-mail"), {
    target: { value: email },
  });
  fireEvent.change(screen.getByLabelText("Hasło"), {
    target: { value: "Haslo123!" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Załóż konto" }));
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("RegisterForm", () => {
  it("labels every field and lets the browser suggest a new password", () => {
    render(<RegisterForm returnUrl={null} />);

    expect(screen.getByLabelText("Imię").getAttribute("autocomplete")).toBe(
      "given-name",
    );
    expect(screen.getByLabelText("Nazwisko").getAttribute("autocomplete")).toBe(
      "family-name",
    );
    expect(
      screen.getByLabelText("Adres e-mail").getAttribute("autocomplete"),
    ).toBe("email");
    expect(screen.getByLabelText("Hasło").getAttribute("autocomplete")).toBe(
      "new-password",
    );
  });

  it("sends the returnUrl on and shows one screen that names no address", async () => {
    const fetchMock = respondWith(202);

    render(<RegisterForm returnUrl="/competitions/abc" />);
    fillAndSubmit("zajety@example.org");

    const status = await screen.findByRole("status");
    expect(status.textContent).toContain("Sprawdź skrzynkę");
    expect(status.textContent).not.toContain("zajety@example.org");
    expect(JSON.parse(fetchMock.mock.calls[0][1].body).returnUrl).toBe(
      "/competitions/abc",
    );
  });

  it("puts the backend's password policy messages under the password", async () => {
    respondWith(400, {
      errors: {
        password: [
          "Hasło musi zawierać co najmniej jedną cyfrę.",
          "Hasło musi zawierać co najmniej jeden znak specjalny.",
        ],
      },
    });

    render(<RegisterForm returnUrl={null} />);
    fillAndSubmit();

    expect((await screen.findByRole("alert")).textContent).toBe(
      "Popraw zaznaczone pola.",
    );
    const password = screen.getByLabelText("Hasło") as HTMLInputElement;
    expect(password.getAttribute("aria-invalid")).toBe("true");
    expect(password.value).toBe("");

    const describedBy = password.getAttribute("aria-describedby") ?? "";
    const described = describedBy
      .split(" ")
      .map((id) => document.getElementById(id)?.textContent ?? "")
      .join(" ");
    expect(described).toContain("co najmniej jedną cyfrę");
    expect(described).toContain("znak specjalny");

    expect(screen.getByLabelText("Imię").getAttribute("aria-invalid")).toBeNull();
  });

  it("keeps a good password when only a name was refused", async () => {
    respondWith(400, { errors: { firstName: ["Imię jest wymagane."] } });

    render(<RegisterForm returnUrl={null} />);
    fillAndSubmit();

    await screen.findByRole("alert");
    expect(screen.getByLabelText("Imię").getAttribute("aria-invalid")).toBe("true");
    expect((screen.getByLabelText("Hasło") as HTMLInputElement).value).toBe(
      "Haslo123!",
    );
  });

  it("offers a new link with the way back when the mail does not arrive", async () => {
    respondWith(202);

    render(<RegisterForm returnUrl="/competitions/abc" />);
    fillAndSubmit();

    expect(
      (
        await screen.findByRole("link", { name: "wyślij link jeszcze raz" })
      ).getAttribute("href"),
    ).toBe("/verify-email?returnUrl=%2Fcompetitions%2Fabc");
  });

  it("passes on the rate limit sentence", async () => {
    respondWith(429, {
      detail: "Zbyt wiele prób z tego adresu. Spróbuj ponownie za chwilę.",
    });

    render(<RegisterForm returnUrl={null} />);
    fillAndSubmit();

    expect((await screen.findByRole("alert")).textContent).toContain(
      "Zbyt wiele prób",
    );
    expect((screen.getByLabelText("Hasło") as HTMLInputElement).value).toBe(
      "Haslo123!",
    );
  });

  it("keeps what was typed and says nothing technical when the server is down", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new TypeError("Failed to fetch")),
    );

    render(<RegisterForm returnUrl={null} />);
    fillAndSubmit();

    expect((await screen.findByRole("alert")).textContent).toBe(
      "Nie udało się teraz połączyć z serwerem. Spróbuj ponownie za chwilę.",
    );
    expect((screen.getByLabelText("Hasło") as HTMLInputElement).value).toBe(
      "Haslo123!",
    );
  });

  it("keeps the way back on the link to signing in", () => {
    render(<RegisterForm returnUrl="/competitions/abc" />);

    expect(
      screen.getByRole("link", { name: "Zaloguj się" }).getAttribute("href"),
    ).toBe("/login?returnUrl=%2Fcompetitions%2Fabc");
  });
});
