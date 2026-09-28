import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";

import { TeamList } from "./team-list";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("TeamList", () => {
  it("shows each member of the team with role and state, and nothing to change", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(async () =>
        new Response(
          JSON.stringify([
            { id: "u1", firstName: "Anna", lastName: "Operatorka", email: "anna@ocwip.pl", role: "Operator", isActive: true },
            { id: "u2", firstName: "Jan", lastName: "Ekspert", email: "jan@example.org", role: "Reviewer", isActive: false },
          ]),
          { status: 200 },
        ),
      ),
    );

    render(<TeamList />);

    const expert = (await screen.findByText("jan@example.org")).closest("tr")!;
    expect(within(expert).getByText("Ekspert")).toBeTruthy();
    expect(within(expert).getByText("wyłączone")).toBeTruthy();
    expect(screen.getByText("anna@ocwip.pl").closest("tr")!.textContent).toContain("aktywne");
    expect(screen.queryByRole("button")).toBeNull();
  });
});
