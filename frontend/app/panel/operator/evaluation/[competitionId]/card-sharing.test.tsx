import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import { CardSharing } from "./card-sharing";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function serve(sharedAt: string | null) {
  const fetchMock = vi.fn().mockImplementation(async (_input: string, init?: RequestInit) =>
    init?.method === "POST"
      ? new Response(JSON.stringify({ sharedAt: "2026-06-01T10:00:00Z" }))
      : new Response(JSON.stringify({ sharedAt })),
  );
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const posts = (fetchMock: ReturnType<typeof vi.fn>) =>
  fetchMock.mock.calls.filter(([, init]) => (init as RequestInit | undefined)?.method === "POST");

describe("CardSharing", () => {
  it("shares only after the confirmation and then says it cannot be undone", async () => {
    const fetchMock = serve(null);

    render(<CardSharing competitionId="c1" />);

    fireEvent.click(await screen.findByRole("button", { name: "Udostępnij karty wnioskodawcom" }));
    expect(posts(fetchMock)).toHaveLength(0);
    expect(screen.getByText(/Tej decyzji nie można cofnąć/)).toBeDefined();

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Udostępnij", hidden: true }));
    });

    expect(posts(fetchMock)).toHaveLength(1);
    expect(await screen.findByText(/Karty oceny udostępniono wnioskodawcom/)).toBeDefined();
    expect(screen.queryByRole("button", { name: "Udostępnij karty wnioskodawcom" })).toBeNull();
  });

  it("goes back without sharing", async () => {
    const fetchMock = serve(null);

    render(<CardSharing competitionId="c1" />);

    fireEvent.click(await screen.findByRole("button", { name: "Udostępnij karty wnioskodawcom" }));
    fireEvent.click(screen.getByRole("button", { name: "Wróć", hidden: true }));

    expect(posts(fetchMock)).toHaveLength(0);
    expect(screen.getByRole("button", { name: "Udostępnij karty wnioskodawcom" })).toBeDefined();
  });

  it("offers nothing to click once the cards are shared", async () => {
    serve("2026-06-01T10:00:00Z");

    render(<CardSharing competitionId="c1" />);

    expect(await screen.findByText(/Karty oceny udostępniono wnioskodawcom/)).toBeDefined();
    expect(screen.queryByRole("button")).toBeNull();
  });
});
