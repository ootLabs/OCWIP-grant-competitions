import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import { AccountLinks } from "./account-links";

function me(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => new Response(body === null ? null : JSON.stringify(body), { status, headers: { "content-type": "application/json" } })),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("AccountLinks", () => {
  it("offers sign in and registration to a visitor", async () => {
    me(null, 401);

    render(<AccountLinks />);

    expect(screen.getByRole("link", { name: "Zaloguj" }).getAttribute("href")).toBe("/login");
    expect(screen.getByRole("link", { name: "Załóż konto" }).getAttribute("href")).toBe("/register");
  });

  it("leads a signed in applicant to their panel", async () => {
    me({ id: "u1", email: "a@example.org", firstName: "Ada", lastName: "T", role: "Applicant", entityName: null, isExpert: false });

    render(<AccountLinks />);

    expect((await screen.findByRole("link", { name: "Mój panel" })).getAttribute("href")).toBe("/panel/applicant");
    expect(screen.queryByRole("link", { name: "Zaloguj" })).toBeNull();
  });
});
