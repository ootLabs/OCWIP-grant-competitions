import { afterEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";

import { LoginForm } from "./login-form";

const replace = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
}));

function respondWith(body: unknown, status: number) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), {
      status,
      headers: {
        "content-type":
          status >= 400 ? "application/problem+json" : "application/json",
      },
    }),
  );
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function fillAndSubmit(email = "biuro@example.org", password = "Haslo123!") {
  fireEvent.change(screen.getByLabelText("Adres e-mail"), {
    target: { value: email },
  });
  fireEvent.change(screen.getByLabelText("Hasło"), {
    target: { value: password },
  });
  fireEvent.click(screen.getByRole("button", { name: "Zaloguj się" }));
}

afterEach(() => {
  cleanup();
  replace.mockReset();
  vi.unstubAllGlobals();
});

describe("LoginForm", () => {
  it("labels both fields and lets the browser fill them in", () => {
    render(<LoginForm returnUrl={null} />);

    expect(screen.getByLabelText("Adres e-mail").getAttribute("autocomplete")).toBe(
      "email",
    );
    expect(screen.getByLabelText("Hasło").getAttribute("autocomplete")).toBe(
      "current-password",
    );
  });

  it("goes where the backend says, not where the address said", async () => {
    const fetchMock = respondWith(
      { redirectPath: "/panel/applicant" },
      200,
    );

    render(<LoginForm returnUrl="https://evil.example" />);
    fillAndSubmit();

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/panel/applicant"));
    expect(JSON.parse(fetchMock.mock.calls[0][1].body).returnUrl).toBe(
      "https://evil.example",
    );
  });

  it("shows one message for wrong credentials and clears the password", async () => {
    respondWith({ detail: "Nieprawidłowy e-mail lub hasło." }, 401);

    render(<LoginForm returnUrl={null} />);
    fillAndSubmit();

    expect((await screen.findByRole("alert")).textContent).toBe(
      "Nieprawidłowy e-mail lub hasło.",
    );
    expect((screen.getByLabelText("Hasło") as HTMLInputElement).value).toBe("");
    expect((screen.getByLabelText("Adres e-mail") as HTMLInputElement).value).toBe(
      "biuro@example.org",
    );
    expect(replace).not.toHaveBeenCalled();
  });

  it("says why an unconfirmed address cannot sign in", async () => {
    respondWith({ detail: "Potwierdź swój adres e-mail, zanim się zalogujesz." }, 403);

    render(<LoginForm returnUrl={null} />);
    fillAndSubmit();

    expect((await screen.findByRole("alert")).textContent).toContain(
      "Potwierdź swój adres e-mail",
    );
  });

  it("passes on how long a lockout lasts", async () => {
    respondWith(
      { detail: "Spróbuj ponownie za około 15 min." },
      429,
    );

    render(<LoginForm returnUrl={null} />);
    fillAndSubmit();

    expect((await screen.findByRole("alert")).textContent).toContain("15 min");
  });

  it("keeps the password and says nothing technical when the server is down", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    render(<LoginForm returnUrl={null} />);
    fillAndSubmit();

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toBe(
      "Nie udało się teraz zalogować. Spróbuj ponownie za chwilę.",
    );
    expect((screen.getByLabelText("Hasło") as HTMLInputElement).value).toBe(
      "Haslo123!",
    );
  });

  it("keeps the way back on the link to registration", () => {
    render(<LoginForm returnUrl="/competitions/abc" />);

    expect(
      screen.getByRole("link", { name: "Załóż konto" }).getAttribute("href"),
    ).toBe("/register?returnUrl=%2Fcompetitions%2Fabc");
    expect(
      screen.getByRole("link", { name: "Nie pamiętam hasła" }).getAttribute("href"),
    ).toBe("/forgot-password");
  });
});
