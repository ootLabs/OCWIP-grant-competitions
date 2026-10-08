import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { emptyCard } from "@/lib/entity-card";

import { EntityCardForm } from "./entity-card-form";

function respondWith(body: unknown, status = 200, contentType = "application/json") {
  const fetch = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), { status, headers: { "content-type": contentType } }),
  );
  vi.stubGlobal("fetch", fetch);
  return fetch;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

const saved = { id: "e1", updatedAt: "2026-09-27T10:00:00Z", card: emptyCard(), isFounder: true, members: [] };

describe("EntityCardForm", () => {
  it("asks an informal group without a patron for its name only, never for a NIP", () => {
    render(<EntityCardForm initial={emptyCard()} entityId={null} submitLabel="Zapisz" onSaved={vi.fn()} />);
    expect(screen.getByLabelText("NIP")).toBeDefined();

    fireEvent.click(screen.getByRole("radio", { name: /Grupa nieformalna\s*Grupa bez osobowości prawnej i bez patrona/ }));

    expect(screen.getByLabelText("Nazwa grupy")).toBeDefined();
    expect(screen.queryByLabelText("NIP")).toBeNull();
    expect(screen.queryByText("Osoby uprawnione do reprezentowania")).toBeNull();
  });

  it("creates the card with POST the first time and hands back what was saved", async () => {
    const fetch = respondWith(saved, 201);
    const onSaved = vi.fn();
    render(<EntityCardForm initial={emptyCard()} entityId={null} submitLabel="Zapisz" onSaved={onSaved} />);

    fireEvent.change(screen.getByLabelText("Pełna nazwa organizacji"), { target: { value: "Fundacja" } });
    fireEvent.click(screen.getByRole("button", { name: "Zapisz" }));

    await vi.waitFor(() => expect(onSaved).toHaveBeenCalledWith(saved));
    const [url, init] = fetch.mock.calls[0];
    expect(String(url)).toMatch(/\/me\/entities$/);
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body).name).toBe("Fundacja");
  });

  it("corrects an existing card with PUT", async () => {
    const fetch = respondWith(saved);
    render(<EntityCardForm initial={emptyCard()} entityId="e1" submitLabel="Zapisz" onSaved={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "Zapisz" }));

    await vi.waitFor(() => expect(fetch).toHaveBeenCalled());
    expect(String(fetch.mock.calls[0][0])).toContain("/me/entities/e1");
    expect(fetch.mock.calls[0][1].method).toBe("PUT");
  });

  it("offers to ask for access instead of a second card when the NIP is taken (T-93a)", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ status: 409, detail: "Ta organizacja jest już zarejestrowana." }), {
          status: 409,
          headers: { "content-type": "application/problem+json" },
        }),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({ id: "r1", entityName: "Fundacja", status: "Pending", requestedAt: "2026-10-07T10:00:00Z", decidedAt: null }),
          { status: 200, headers: { "content-type": "application/json" } },
        ),
      );
    vi.stubGlobal("fetch", fetch);
    const onSaved = vi.fn();
    const onAccessRequested = vi.fn();
    render(
      <EntityCardForm
        initial={{ ...emptyCard(), name: "Fundacja", nip: "526-000-12-46" }}
        entityId={null}
        submitLabel="Zapisz"
        onSaved={onSaved}
        onAccessRequested={onAccessRequested}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Zapisz" }));

    expect(await screen.findByText("Ta organizacja jest już zarejestrowana.")).toBeDefined();
    fireEvent.click(screen.getByRole("button", { name: "Poproś o dostęp" }));

    expect(await screen.findByText("Prośba o dostęp została wysłana.")).toBeDefined();
    const [url, init] = fetch.mock.calls[1];
    expect(String(url)).toContain("/me/access-requests");
    expect(JSON.parse(init.body)).toEqual({ nip: "526-000-12-46" });
    expect(onAccessRequested).toHaveBeenCalled();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("marks the NIP, without offering access, when a correction hits a taken NIP", async () => {
    respondWith(
      { status: 409, detail: "Ta organizacja jest już zarejestrowana. Możesz poprosić o dostęp." },
      409,
      "application/problem+json",
    );
    render(
      <EntityCardForm
        initial={{ ...emptyCard(), name: "Fundacja", nip: "526-000-12-46" }}
        entityId="e1"
        submitLabel="Zapisz"
        onSaved={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Zapisz" }));

    expect((await screen.findByRole("alert")).textContent).toBe("Popraw zaznaczone pola.");
    expect(screen.getByLabelText("NIP").getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByText("Ten NIP ma już inna karta organizacji. Sprawdź numer.")).toBeDefined();
    expect(screen.queryByRole("button", { name: "Poproś o dostęp" })).toBeNull();
  });

  it("puts the backend's reason next to the field it is about", async () => {
    respondWith(
      { title: "One or more validation errors occurred.", status: 400, errors: { nip: ["To nie jest poprawny NIP."] } },
      400,
      "application/problem+json",
    );
    const onSaved = vi.fn();
    render(<EntityCardForm initial={emptyCard()} entityId={null} submitLabel="Zapisz" onSaved={onSaved} />);

    fireEvent.click(screen.getByRole("button", { name: "Zapisz" }));

    expect((await screen.findByRole("alert")).textContent).toBe("Popraw zaznaczone pola.");
    expect(screen.getByLabelText("NIP").getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByText("To nie jest poprawny NIP.")).toBeDefined();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("reveals the name of the legal form only for \"Inna\"", () => {
    render(<EntityCardForm initial={emptyCard()} entityId={null} submitLabel="Zapisz" onSaved={vi.fn()} />);
    expect(screen.queryByLabelText("Nazwa formy prawnej")).toBeNull();

    fireEvent.change(screen.getByLabelText("Forma prawna"), { target: { value: "Other" } });

    expect(screen.getByLabelText("Nazwa formy prawnej")).toBeDefined();
  });
});
