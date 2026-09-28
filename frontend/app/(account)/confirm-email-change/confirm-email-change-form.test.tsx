import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { ConfirmEmailChangeForm } from "./confirm-email-change-form";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ConfirmEmailChangeForm", () => {
  it("sends nothing until the button, then confirms the link's address", async () => {
    const fetch = vi.fn().mockImplementation(async () => new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetch);
    render(<ConfirmEmailChangeForm userId="u1" email="nowy@example.org" token="t" />);

    expect(fetch).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Potwierdź zmianę adresu" }));

    expect(await screen.findByText(/Adres zmieniony na nowy@example.org/)).toBeTruthy();
    expect(JSON.parse(String(fetch.mock.calls[0][1].body))).toEqual({ userId: "u1", email: "nowy@example.org", token: "t" });
  });

  it("says an incomplete link is incomplete and offers nothing to press", () => {
    render(<ConfirmEmailChangeForm userId={null} email="nowy@example.org" token={null} />);

    expect(screen.getByRole("alert").textContent).toContain("niepełny");
    expect((screen.getByRole("button") as HTMLButtonElement).disabled).toBe(true);
  });
});
