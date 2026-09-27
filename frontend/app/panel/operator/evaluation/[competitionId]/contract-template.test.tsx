import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import { ContractTemplateEditor } from "./contract-template";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

const current = {
  id: "t1",
  competitionId: "c1",
  versionNumber: 1,
  body: "Umowa {{numer_umowy}}, rachunek {{numer_rachunku}}",
  placeholders: [
    { name: "numer_umowy", label: "Numer umowy (numer wniosku)", system: true },
    { name: "numer_rachunku", label: "Numer rachunku", system: false },
  ],
  createdAt: "2026-05-01T10:00:00Z",
};

describe("ContractTemplateEditor", () => {
  it("shows the version in force and what is left to type in", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify(current))));

    render(<ContractTemplateEditor competitionId="c1" />);

    expect(await screen.findByText(/Obowiązuje wersja 1/)).toBeDefined();
    expect(screen.getByText("Do wpisania przy każdej umowie: Numer rachunku.")).toBeDefined();
    expect((screen.getByRole("button", { name: "Opublikuj nową wersję wzoru" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("shows why the server refused a template", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(async (_input: string, init?: RequestInit) =>
        init?.method === "POST"
          ? new Response(JSON.stringify({ title: "x", errors: { body: ["We wzorze jest nawias {{ albo }}, który nie tworzy znacznika."] } }), {
              status: 400,
              headers: { "Content-Type": "application/problem+json" },
            })
          : new Response(JSON.stringify(current)),
      ),
    );

    render(<ContractTemplateEditor competitionId="c1" />);
    fireEvent.change(await screen.findByLabelText("Treść wzoru umowy"), { target: { value: "Umowa {{ Numer }}" } });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Opublikuj nową wersję wzoru" }));
    });

    expect(screen.getByRole("status").textContent).toContain("nie tworzy znacznika");
  });
});
