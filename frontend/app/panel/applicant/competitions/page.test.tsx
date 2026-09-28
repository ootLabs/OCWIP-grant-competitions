import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import CompetitionsPage from "./page";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("Aktualne konkursy (wnioskodawca)", () => {
  it("lists the calls taking applications now and leads to their public pages", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        new Response(
          JSON.stringify([
            { id: "open", number: "1/2026", title: "Otwarty", intake: { acceptsApplications: true, message: "Nabór trwa do 30 października." } },
            { id: "shut", number: "2/2025", title: "Zamknięty", intake: { acceptsApplications: false, message: "Nabór zamknięty." } },
          ]),
          { status: 200, headers: { "content-type": "application/json" } },
        ),
      ),
    );

    render(await CompetitionsPage());

    expect(screen.getByText("1/2026 - Otwarty")).toBeDefined();
    expect(screen.getByText("Nabór trwa do 30 października.")).toBeDefined();
    expect(screen.getByRole("link", { name: "Zobacz konkurs" }).getAttribute("href")).toBe("/competitions/open");
    expect(screen.queryByText("2/2025 - Zamknięty")).toBeNull();
  });
});
