import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import { ContractBundle } from "./contract-bundle";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("ContractBundle", () => {
  it("posts for the ZIP and saves it under the name the server gave", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(new Blob(["zip"]), {
        status: 200,
        headers: { "content-type": "application/zip", "content-disposition": 'attachment; filename="umowy-1-2026.zip"' },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);
    const createObjectURL = vi.fn().mockReturnValue("blob:umowy");
    vi.stubGlobal("URL", Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() }));
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);

    render(<ContractBundle competitionId="c1" />);
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Pobierz wszystkie umowy (ZIP)" }));
    });

    const [input, init] = fetchMock.mock.calls[0]!;
    expect(String(input)).toMatch(/\/competitions\/c1\/contracts\/bundle$/);
    expect((init as RequestInit).method).toBe("POST");
    expect(click).toHaveBeenCalled();
    expect((click.mock.instances[0] as unknown as HTMLAnchorElement).download).toBe("umowy-1-2026.zip");
    expect(screen.getByRole("status").textContent).toContain("braki.txt");
  });

  it("says why when there is nothing to draw up", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ detail: "Konkurs nie ma jeszcze dofinansowanych wniosków." }), {
          status: 409,
          headers: { "content-type": "application/problem+json" },
        }),
      ),
    );

    render(<ContractBundle competitionId="c1" />);
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Pobierz wszystkie umowy (ZIP)" }));
    });

    expect(screen.getByRole("status").textContent).toBe("Konkurs nie ma jeszcze dofinansowanych wniosków.");
  });
});
