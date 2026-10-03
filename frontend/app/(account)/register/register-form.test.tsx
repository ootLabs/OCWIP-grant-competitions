import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { RegisterForm } from "./register-form";

const consents = [
  { kind: "terms", title: "Regulamin serwisu", version: "aaaa", text: "# Regulamin serwisu\n\nTreść." },
  { kind: "privacy", title: "Klauzula informacyjna", version: "bbbb", text: "# Klauzula informacyjna\n\nTreść." },
];

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

function fillAndSubmit(
  email = "biuro@example.org",
  repeats: { email?: string; password?: string } = {},
) {
  const password = "Haslo123!";

  fireEvent.change(screen.getByLabelText("Imię"), { target: { value: "Ada" } });
  fireEvent.change(screen.getByLabelText("Nazwisko"), {
    target: { value: "Nowak" },
  });
  fireEvent.change(screen.getByLabelText("Adres e-mail"), {
    target: { value: email },
  });
  fireEvent.change(screen.getByLabelText("Powtórz adres e-mail"), {
    target: { value: repeats.email ?? email },
  });
  fireEvent.change(screen.getByLabelText("Hasło"), {
    target: { value: password },
  });
  fireEvent.change(screen.getByLabelText("Powtórz hasło"), {
    target: { value: repeats.password ?? password },
  });
  fireEvent.click(screen.getByLabelText("Akceptuję: Regulamin serwisu"));
  fireEvent.click(screen.getByLabelText("Akceptuję: Klauzula informacyjna"));
  fireEvent.click(screen.getByRole("button", { name: "Załóż konto" }));
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("RegisterForm", () => {
  it("labels every field and lets the browser suggest a new password", () => {
    render(<RegisterForm consents={consents} returnUrl={null} />);

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

    render(<RegisterForm consents={consents} returnUrl="/competitions/abc" />);
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

    render(<RegisterForm consents={consents} returnUrl={null} />);
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

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit();

    await screen.findByRole("alert");
    expect(screen.getByLabelText("Imię").getAttribute("aria-invalid")).toBe("true");
    expect((screen.getByLabelText("Hasło") as HTMLInputElement).value).toBe(
      "Haslo123!",
    );
  });

  it("offers a new link with the way back when the mail does not arrive", async () => {
    respondWith(202);

    render(<RegisterForm consents={consents} returnUrl="/competitions/abc" />);
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

    render(<RegisterForm consents={consents} returnUrl={null} />);
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

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit();

    expect((await screen.findByRole("alert")).textContent).toBe(
      "Nie udało się teraz połączyć z serwerem. Spróbuj ponownie za chwilę.",
    );
    expect((screen.getByLabelText("Hasło") as HTMLInputElement).value).toBe(
      "Haslo123!",
    );
  });

  it("keeps the way back on the link to signing in", () => {
    render(<RegisterForm consents={consents} returnUrl="/competitions/abc" />);

    expect(
      screen.getByRole("link", { name: "Zaloguj się" }).getAttribute("href"),
    ).toBe("/login?returnUrl=%2Fcompetitions%2Fabc");
  });

  it("shows each document in full and sends back the versions ticked", async () => {
    const fetchMock = respondWith(202);

    render(<RegisterForm consents={consents} returnUrl={null} />);
    expect(screen.getByRole("region", { name: "Regulamin serwisu" }).textContent).toContain("Treść.");
    fillAndSubmit();

    await screen.findByRole("status");
    expect(JSON.parse(fetchMock.mock.calls[0][1].body).acceptedConsents).toEqual(["aaaa", "bbbb"]);
  });

  it("stops the request when the repeated address differs", () => {
    const fetchMock = respondWith(202);

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit("biuro@example.org", { email: "biuro@exampel.org" });

    expect(fetchMock).not.toHaveBeenCalled();
    expect(screen.getByRole("alert").textContent).toBe("Popraw zaznaczone pola.");

    const repeat = screen.getByLabelText("Powtórz adres e-mail");
    expect(repeat.getAttribute("aria-invalid")).toBe("true");
    const describedBy = repeat.getAttribute("aria-describedby") ?? "";
    expect(document.getElementById(describedBy)?.textContent).toContain(
      "Adresy e-mail są różne",
    );
  });

  it("stops the request when the repeated password differs", () => {
    const fetchMock = respondWith(202);

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit("biuro@example.org", { password: "Haslo123?" });

    expect(fetchMock).not.toHaveBeenCalled();
    const repeat = screen.getByLabelText("Powtórz hasło");
    expect(repeat.getAttribute("aria-invalid")).toBe("true");
    const describedBy = repeat.getAttribute("aria-describedby") ?? "";
    expect(document.getElementById(describedBy)?.textContent).toContain(
      "Hasła są różne",
    );
  });

  // Both messages used to be an answer given once on sending, so they stayed
  // under a corrected box until the next send. At the password, where the
  // typed text is invisible, there was no way to tell whether the message was
  // still true (znalezisko 2).
  it("drops the password message as soon as both boxes match again", () => {
    respondWith(202);

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit("biuro@example.org", { password: "Haslo123?" });

    expect(screen.getByLabelText("Powtórz hasło").getAttribute("aria-invalid")).toBe("true");

    fireEvent.change(screen.getByLabelText("Powtórz hasło"), {
      target: { value: "Haslo123!" },
    });

    expect(screen.getByLabelText("Powtórz hasło").getAttribute("aria-invalid")).toBeNull();
    expect(screen.queryByText("Hasła są różne. Wpisz je jeszcze raz.")).toBeNull();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  // Corrected the other way round: the first box is the one with the typo.
  it("drops the address message when the first box is the corrected one", () => {
    respondWith(202);

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit("biuro@exampel.org", { email: "biuro@example.org" });

    expect(screen.getByLabelText("Powtórz adres e-mail").getAttribute("aria-invalid")).toBe("true");

    fireEvent.change(screen.getByLabelText("Adres e-mail"), {
      target: { value: "biuro@example.org" },
    });

    expect(screen.getByLabelText("Powtórz adres e-mail").getAttribute("aria-invalid")).toBeNull();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("says it again when a correction breaks the pair a second time", () => {
    respondWith(202);

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit("biuro@example.org", { password: "Haslo123?" });
    fireEvent.change(screen.getByLabelText("Powtórz hasło"), {
      target: { value: "Haslo123!" },
    });
    fireEvent.change(screen.getByLabelText("Powtórz hasło"), {
      target: { value: "Haslo123" },
    });

    expect(screen.getByLabelText("Powtórz hasło").getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByRole("alert").textContent).toBe("Popraw zaznaczone pola.");
  });

  it("takes an address repeated in another case, because it is one account", async () => {
    const fetchMock = respondWith(202);

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit("biuro@example.org", { email: "Biuro@Example.org" });

    expect(await screen.findByRole("status")).toBeTruthy();
    expect(JSON.parse(fetchMock.mock.calls[0][1].body).email).toBe(
      "biuro@example.org",
    );
  });

  it("sends neither repeat to the backend", async () => {
    const fetchMock = respondWith(202);

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit();

    await screen.findByRole("status");
    const body = JSON.parse(fetchMock.mock.calls[0][1].body);
    expect(body.emailRepeat).toBeUndefined();
    expect(body.passwordRepeat).toBeUndefined();
  });

  it("clears both password boxes when the policy refused the password", async () => {
    respondWith(400, { errors: { password: ["Hasło musi zawierać cyfrę."] } });

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit();

    await screen.findByRole("alert");
    expect((screen.getByLabelText("Hasło") as HTMLInputElement).value).toBe("");
    expect(
      (screen.getByLabelText("Powtórz hasło") as HTMLInputElement).value,
    ).toBe("");
  });

  it("puts the refusal of a missing consent next to the boxes", async () => {
    respondWith(400, { errors: { acceptedConsents: ["Zaakceptuj: Klauzula informacyjna."] } });

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit();

    expect(await screen.findByText("Zaakceptuj: Klauzula informacyjna.")).toBeTruthy();
    expect((screen.getByLabelText("Hasło") as HTMLInputElement).value).toBe("Haslo123!");
  });

  // The refusal used to be a bare <p>: visible, but announced by nothing and
  // pointing at nothing, so a screen reader heard "Popraw zaznaczone pola" and
  // found no field marked, because the unticked boxes were the only problem.
  it("ties the refusal of a missing consent to the boxes it is about", async () => {
    respondWith(400, { errors: { acceptedConsents: ["Zaakceptuj: Klauzula informacyjna."] } });

    render(<RegisterForm consents={consents} returnUrl={null} />);
    fillAndSubmit();

    const refusal = await screen.findByText("Zaakceptuj: Klauzula informacyjna.");
    const box = screen.getByLabelText("Akceptuję: Regulamin serwisu");

    expect(refusal.getAttribute("role")).toBe("alert");
    expect(box.getAttribute("aria-invalid")).toBe("true");
    expect(box.getAttribute("aria-describedby")).toBe(refusal.id);
    expect(refusal.id).not.toBe("");
  });

  // The backend serves the file as it is, heading line included, because the
  // version is the hash of that whole text. The box printed the "#".
  it("shows the consent text without its markdown heading", () => {
    render(<RegisterForm consents={consents} returnUrl={null} />);

    const box = screen.getByRole("region", { name: "Regulamin serwisu" });

    expect(box.textContent).toBe("Treść.");
    expect(box.textContent).not.toContain("#");
  });
});
