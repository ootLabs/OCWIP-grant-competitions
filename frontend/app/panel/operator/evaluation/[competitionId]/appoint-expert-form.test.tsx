import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { AppointExpertForm } from "./appoint-expert-form";

const expert = (pending: boolean) => ({
  userId: "u1",
  firstName: "Ewa",
  lastName: "Ekspercka",
  email: "ewa@example.org",
  appointedAt: "2026-10-08T10:00:00Z",
  invitationPending: pending,
  assigned: 0,
});

function json(body: unknown, status: number) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": status >= 400 ? "application/problem+json" : "application/json" },
  });
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("Powołanie do komisji (R-44)", () => {
  it("appoints an existing account by address", async () => {
    const fetch = vi.fn().mockResolvedValue(json(expert(false), 200));
    vi.stubGlobal("fetch", fetch);
    const onAppointed = vi.fn();
    render(<AppointExpertForm competitionId="c1" onAppointed={onAppointed} />);

    fireEvent.change(screen.getByLabelText("Adres e-mail eksperta"), { target: { value: "ewa@example.org" } });
    fireEvent.click(screen.getByRole("button", { name: "Powołaj do komisji" }));

    expect((await screen.findByRole("status")).textContent).toContain("Powołano: Ewa Ekspercka");
    expect(onAppointed).toHaveBeenCalled();
    expect(JSON.parse(String(fetch.mock.calls[0][1].body))).toEqual({ email: "ewa@example.org" });
  });

  it("asks for a name when the address has no account, then invites", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(json({ status: 404, detail: "Nie ma konta z tym adresem." }, 404))
      .mockResolvedValueOnce(json(expert(true), 201));
    vi.stubGlobal("fetch", fetch);
    render(<AppointExpertForm competitionId="c1" onAppointed={vi.fn()} />);

    fireEvent.change(screen.getByLabelText("Adres e-mail eksperta"), { target: { value: "ewa@example.org" } });
    fireEvent.click(screen.getByRole("button", { name: "Powołaj do komisji" }));

    fireEvent.change(await screen.findByLabelText("Imię"), { target: { value: "Ewa" } });
    fireEvent.change(screen.getByLabelText("Nazwisko"), { target: { value: "Ekspercka" } });
    fireEvent.click(screen.getByRole("button", { name: "Zaproś do komisji" }));

    expect((await screen.findByRole("status")).textContent).toContain("Wysłano zaproszenie");
    expect(JSON.parse(String(fetch.mock.calls[1][1].body))).toEqual({
      email: "ewa@example.org",
      firstName: "Ewa",
      lastName: "Ekspercka",
    });
  });
});
