import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

const fetchConsents = vi.fn();
vi.mock("@/lib/account", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/account")>()),
  fetchConsents: () => fetchConsents(),
}));

const { LegalDocumentPage } = await import("./legal-document");

afterEach(cleanup);

describe("LegalDocumentPage", () => {
  it("shows the document accepted at registration, its first line as the heading", async () => {
    fetchConsents.mockResolvedValue([
      { kind: "terms", title: "Regulamin serwisu", version: "a", text: "# Regulamin serwisu\n\nPierwszy akapit.\n\n1. Punkt." },
      { kind: "privacy", title: "Klauzula", version: "b", text: "# Klauzula\n\nInna treść." },
    ]);

    render(await LegalDocumentPage({ kind: "terms" }));

    expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Regulamin serwisu");
    expect(screen.getByText("Pierwszy akapit.")).toBeDefined();
    expect(screen.getByText("1. Punkt.")).toBeDefined();
    expect(screen.queryByText("Inna treść.")).toBeNull();
  });

  it("keeps a first line that is not a heading, since the server titled it by kind", async () => {
    fetchConsents.mockResolvedValue([{ kind: "privacy", title: "privacy", version: "c", text: "Administratorem danych jest OCWIP.\n\nDrugi akapit." }]);

    render(await LegalDocumentPage({ kind: "privacy" }));

    expect(screen.getByText("Administratorem danych jest OCWIP.")).toBeDefined();
    expect(screen.getByText("Drugi akapit.")).toBeDefined();
  });

  it("says the server is unavailable instead of a blank page", async () => {
    fetchConsents.mockRejectedValue(new TypeError("fetch failed"));

    render(await LegalDocumentPage({ kind: "privacy" }));

    expect(screen.getByRole("alert")).toBeDefined();
  });
});
