import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import { ResultMails } from "./result-mails";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function serve(state: Record<string, unknown>, afterSend: Record<string, unknown>) {
  const fetchMock = vi.fn().mockImplementation(async (input: string, init?: RequestInit) => {
    const path = new URL(String(input), "http://localhost").pathname.replace(/^\/api/, "");
    if (path.endsWith("/result-messages") && init?.method === "PUT") return new Response(init.body as string);
    if (path.endsWith("/result-messages")) return new Response(JSON.stringify({ funded: "Gratulacje!", reserve: null, rejected: null }));
    if (path.endsWith("/send")) return new Response(JSON.stringify(afterSend));
    return new Response(JSON.stringify(state));
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const counts = (total: number, sent: number, failed = 0) => ({
  total,
  sent,
  pending: total - sent,
  failed,
  lastSentAt: sent > 0 ? "2026-06-01T10:00:00Z" : null,
});

describe("ResultMails", () => {
  it("saves the three texts and offers no sending before the approval", async () => {
    const fetchMock = serve(counts(0, 0), counts(0, 0));

    render(<ResultMails competitionId="c1" approved={false} />);

    const funded = await screen.findByDisplayValue("Gratulacje!");
    fireEvent.change(screen.getByLabelText("Wniosek bez dofinansowania"), { target: { value: "Dziękujemy za udział." } });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Zapisz treści" }));
    });

    const put = fetchMock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === "PUT")!;
    expect(JSON.parse((put[1] as RequestInit).body as string)).toEqual({
      funded: "Gratulacje!",
      reserve: "",
      rejected: "Dziękujemy za udział.",
    });
    expect(funded).toBeDefined();
    expect(screen.queryByRole("button", { name: /Wyślij/ })).toBeNull();
  });

  it("sends after the approval and says to send again when some mails failed", async () => {
    serve(counts(3, 0), counts(3, 2, 1));

    render(<ResultMails competitionId="c1" approved />);

    await act(async () => {
      fireEvent.click(await screen.findByRole("button", { name: "Wyślij wiadomości o wynikach" }));
    });

    expect(screen.getByText(/Wysłano 2 z 3, nie udało się: 1/)).toBeDefined();
    expect(screen.getByRole("status").textContent).toMatch(/Wyślij ponownie/);
    expect(screen.getByRole("button", { name: "Wyślij brakujące wiadomości" })).toBeDefined();
  });

  it("keeps a text being typed when the results get approved", async () => {
    serve(counts(3, 0), counts(3, 3));

    const { rerender } = render(<ResultMails competitionId="c1" approved={false} />);
    await screen.findByDisplayValue("Gratulacje!");
    fireEvent.change(screen.getByLabelText("Wniosek na liście rezerwowej"), { target: { value: "Jesteś na liście." } });

    await act(async () => {
      rerender(<ResultMails competitionId="c1" approved />);
    });

    expect(await screen.findByRole("button", { name: "Wyślij wiadomości o wynikach" })).toBeDefined();
    expect(screen.getByDisplayValue("Jesteś na liście.")).toBeDefined();
  });
});
