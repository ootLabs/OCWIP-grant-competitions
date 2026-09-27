import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import { contractFixture } from "@/lib/contracts.fixtures";

import { ContractPanel } from "./contract-panel";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function serve(initial: unknown, initialStatus = 200) {
  const fetchMock = vi.fn().mockImplementation(async (input: string, init?: RequestInit) => {
    const path = new URL(String(input), "http://localhost").pathname;
    if (init?.method === "POST" && path.endsWith("/contract")) return new Response(JSON.stringify(contractFixture()), { status: 201 });
    if (init?.method === "PUT") {
      const values = JSON.parse(init.body as string).values as Record<string, string>;
      return new Response(JSON.stringify(contractFixture({ fields: [contractFixture().fields[0], { name: "numer_rachunku", label: "Numer rachunku", system: false, value: values.numer_rachunku }] })));
    }
    if (path.endsWith("/sign")) {
      return new Response(JSON.stringify(contractFixture({ status: "Signed", signedOn: "2026-05-04", fields: [contractFixture().fields[0], { name: "numer_rachunku", label: "Numer rachunku", system: false, value: "12 3456" }] })));
    }
    return new Response(JSON.stringify(initial), { status: initialStatus });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

describe("ContractPanel", () => {
  it("draws the contract up when there is none", async () => {
    serve({ title: "Nie ma takiej umowy." }, 404);

    render(<ContractPanel applicationId="a1" />);
    await act(async () => {
      fireEvent.click(await screen.findByRole("button", { name: "Przygotuj umowę" }));
    });

    expect(await screen.findByLabelText("Numer rachunku")).toBeDefined();
    expect(screen.getByText("1/2026/1")).toBeDefined();
  });

  it("records the signing only with a date and after the confirmation, saving the values first", async () => {
    const fetchMock = serve(contractFixture());

    render(<ContractPanel applicationId="a1" />);
    fireEvent.change(await screen.findByLabelText("Numer rachunku"), { target: { value: "12 3456" } });

    const sign = screen.getByRole("button", { name: "Zapisz podpisanie umowy" });
    expect((sign as HTMLButtonElement).disabled).toBe(true);
    fireEvent.change(screen.getByLabelText("Data podpisania"), { target: { value: "2026-05-04" } });
    fireEvent.click(sign);
    expect(fetchMock.mock.calls.some(([input]) => String(input).endsWith("/sign"))).toBe(false);

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Zapisz podpisanie", hidden: true }));
    });

    const paths = fetchMock.mock.calls.map(([input, init]) => `${(init as RequestInit | undefined)?.method ?? "GET"} ${new URL(String(input), "http://localhost").pathname}`);
    expect(paths.slice(-2)).toEqual(["PUT /contracts/k1/values", "POST /contracts/k1/sign"]);
    expect(JSON.parse(fetchMock.mock.calls.at(-1)![1].body as string)).toEqual({ signedOn: "2026-05-04" });
    expect(await screen.findByText(/Podpisana/)).toBeDefined();
    expect(screen.queryByLabelText("Numer rachunku")).toBeNull();
  });
});
