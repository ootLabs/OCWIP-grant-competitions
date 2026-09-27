import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";

import { contractFixture } from "@/lib/contracts.fixtures";

import { ContractEntry } from "./contract-entry";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ContractEntry", () => {
  it("offers the signed contract to download with the day it was signed", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify(contractFixture({ status: "Signed", signedOn: "2026-05-04" })))));

    render(<ContractEntry applicationId="a1" />);

    expect(await screen.findByText(/Umowa podpisana/)).toBeDefined();
    expect(screen.getByRole("link", { name: "Pobierz umowę (PDF)" }).getAttribute("href")).toMatch(/\/contracts\/k1\/pdf$/);
  });

  it("says nothing before the contract is drawn up", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ title: "Nie ma takiej umowy." }), { status: 404 }));
    vi.stubGlobal("fetch", fetchMock);

    const { container } = render(<ContractEntry applicationId="a1" />);

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(container.textContent).toBe("");
  });
});
