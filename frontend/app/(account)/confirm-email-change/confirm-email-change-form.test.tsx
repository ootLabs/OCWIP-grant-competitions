import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { ConfirmEmailChangeForm } from "./confirm-email-change-form";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ConfirmEmailChangeForm", () => {
  // The address used to travel in the link and in this request; it waits on
  // the account instead, so neither carries it (obserwacja 2).
  it("sends nothing until the button, then confirms with the id and the token only", async () => {
    const fetch = vi.fn().mockImplementation(async () => new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetch);
    render(<ConfirmEmailChangeForm userId="u1" token="t" />);

    expect(fetch).not.toHaveBeenCalled();
    expect(document.body.textContent).not.toContain("@");
    fireEvent.click(screen.getByRole("button", { name: "Potwierdź zmianę adresu" }));

    expect(await screen.findByText(/Adres konta został zmieniony/)).toBeTruthy();
    expect(JSON.parse(String(fetch.mock.calls[0][1].body))).toEqual({ userId: "u1", token: "t" });
  });

  it("says an incomplete link is incomplete and offers nothing to press", () => {
    render(<ConfirmEmailChangeForm userId={null} token={null} />);

    expect(screen.getByRole("alert").textContent).toContain("niepełny");
    expect((screen.getByRole("button") as HTMLButtonElement).disabled).toBe(true);
  });
});
