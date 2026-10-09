import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { AccountSettings } from "./account-settings";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function respond(status: number, body?: unknown) {
  const calls: { url: string; init?: RequestInit }[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async (url: string, init?: RequestInit) => {
      calls.push({ url, init });
      return body === undefined
        ? new Response(null, { status })
        : new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/problem+json" } });
    }),
  );
  return calls;
}

describe("AccountSettings", () => {
  it("changes the password with the current one and says the other devices are signed out", async () => {
    const calls = respond(204);
    render(<AccountSettings />);

    fireEvent.change(screen.getAllByLabelText("Obecne hasło")[0], { target: { value: "stare" } });
    fireEvent.change(screen.getByLabelText(/^Nowe hasło/), { target: { value: "Nowe!Haslo1" } });
    fireEvent.change(screen.getByLabelText(/^Powtórz nowe hasło/), { target: { value: "Nowe!Haslo1" } });
    fireEvent.click(screen.getByRole("button", { name: "Zmień hasło" }));

    expect(await screen.findByText(/Inne urządzenia zostały wylogowane/)).toBeTruthy();
    expect(calls[0].url).toContain("/me/password");
    expect(JSON.parse(String(calls[0].init!.body))).toEqual({ currentPassword: "stare", newPassword: "Nowe!Haslo1" });
  });

  it("sends nothing while the repeated new password differs, and says so at the field (P4-10)", async () => {
    const calls = respond(204);
    render(<AccountSettings />);

    fireEvent.change(screen.getAllByLabelText("Obecne hasło")[0], { target: { value: "stare" } });
    fireEvent.change(screen.getByLabelText(/^Nowe hasło/), { target: { value: "Nowe!Haslo1" } });
    fireEvent.change(screen.getByLabelText(/^Powtórz nowe hasło/), { target: { value: "Nowe!Haslo2" } });
    fireEvent.click(screen.getByRole("button", { name: "Zmień hasło" }));

    expect(await screen.findByText("Hasła są różne. Wpisz je jeszcze raz.")).toBeTruthy();
    expect(calls).toHaveLength(0);

    fireEvent.change(screen.getByLabelText(/^Powtórz nowe hasło/), { target: { value: "Nowe!Haslo1" } });
    expect(screen.queryByText("Hasła są różne. Wpisz je jeszcze raz.")).toBeNull();
  });

  it("shows the server's refusal of the current password next to the field", async () => {
    respond(400, { title: "Błąd", errors: { currentPassword: ["Obecne hasło jest nieprawidłowe."] } });
    render(<AccountSettings />);

    fireEvent.change(screen.getAllByLabelText("Obecne hasło")[0], { target: { value: "zle" } });
    fireEvent.change(screen.getByLabelText(/^Nowe hasło/), { target: { value: "Nowe!Haslo1" } });
    fireEvent.change(screen.getByLabelText(/^Powtórz nowe hasło/), { target: { value: "Nowe!Haslo1" } });
    fireEvent.click(screen.getByRole("button", { name: "Zmień hasło" }));

    expect(await screen.findByText("Obecne hasło jest nieprawidłowe.")).toBeTruthy();
  });

  it("asks for a new address and says it changes only after the link", async () => {
    const calls = respond(204);
    render(<AccountSettings />);

    fireEvent.change(screen.getByLabelText("Nowy adres e-mail"), { target: { value: "nowy@example.org" } });
    fireEvent.change(screen.getAllByLabelText("Obecne hasło")[1], { target: { value: "haslo" } });
    fireEvent.click(screen.getByRole("button", { name: "Zmień adres" }));

    expect(await screen.findByText(/zmieni się dopiero po jego otwarciu/)).toBeTruthy();
    expect(JSON.parse(String(calls[0].init!.body))).toEqual({ newEmail: "nowy@example.org", currentPassword: "haslo" });
  });
});
