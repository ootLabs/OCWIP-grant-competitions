import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  useParams: () => ({ applicationId: "app-1" }),
}));

vi.mock("./draft-workspace", () => ({
  DraftWorkspace: ({ onSubmitted }: { onSubmitted: (application: unknown) => void }) => (
    <div>
      <p>tryb wypełniania</p>
      <button
        type="button"
        onClick={() =>
          onSubmitted({
            id: "app-1",
            competitionId: "c1",
            status: "Submitted",
            number: "001",
            submittedAt: "2026-09-20T10:00:00Z",
            lastSavedAt: "2026-09-20T10:00:00Z",
            checksum: "next",
            isActive: true,
            answers: {},
          })
        }
      >
        symuluj złożenie
      </button>
    </div>
  ),
}));

vi.mock("./submitted-view", () => ({
  SubmittedView: () => <p>tryb podglądu złożonego wniosku</p>,
}));

import ApplicationPage from "./page";

const application = {
  id: "app-1",
  competitionId: "c1",
  formDefinitionId: "f1",
  status: "Draft",
  answers: {},
  number: null,
  submittedAt: null,
  lastSavedAt: "2026-09-12T10:32:00Z",
  checksum: "0a55-22c2-b414",
  isActive: true,
};

const form = { versionNumber: 1, definition: { schemaVersion: 1, sections: [] } };

const competition = {
  id: "c1",
  number: "1/2026",
  title: "Konkurs testowy",
  intake: { acceptsApplications: true, state: "Open", opensAt: null, closesAt: null, message: "Nabór trwa." },
  attachments: [],
};

function respondByPath(overrides: Record<string, { body: unknown; status?: number }> = {}) {
  const routes: Record<string, { body: unknown; status?: number }> = {
    "/applications/app-1": { body: application },
    "/applications/app-1/form-definition": { body: form },
    "/public/competitions/c1": { body: competition },
    "/applications/app-1/attachments": { body: [] },
    ...overrides,
  };

  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async (input: string) => {
      const path = new URL(input, "http://localhost").pathname;
      const route = routes[path];
      if (route === undefined) {
        throw new Error(`Unhandled path in test: ${path}`);
      }
      return new Response(JSON.stringify(route.body), { status: route.status ?? 200 });
    }),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ApplicationPage", () => {
  it("opens the draft workspace for a draft application", async () => {
    respondByPath();

    render(<ApplicationPage />);

    expect(await screen.findByText("tryb wypełniania")).toBeDefined();
  });

  it("opens the read only view for a submitted application", async () => {
    respondByPath({ "/applications/app-1": { body: { ...application, status: "Submitted" } } });

    render(<ApplicationPage />);

    expect(await screen.findByText("tryb podglądu złożonego wniosku")).toBeDefined();
  });

  it("switches to the read only view once DraftWorkspace reports a submission, without a refetch", async () => {
    respondByPath();
    render(<ApplicationPage />);

    const fireButton = await screen.findByRole("button", { name: "symuluj złożenie" });
    fireEvent.click(fireButton);

    expect(await screen.findByText("tryb podglądu złożonego wniosku")).toBeDefined();
  });

  it("says the address leads nowhere when the application is not the caller's own", async () => {
    respondByPath({ "/applications/app-1": { body: { title: "Nie masz dostępu." }, status: 403 } });

    render(<ApplicationPage />);

    expect(await screen.findByText("Nie ma takiego wniosku")).toBeDefined();
    expect(screen.getByRole("link", { name: "Wróć do listy wniosków" }).getAttribute("href")).toBe(
      "/panel/applicant",
    );
  });

  it("offers a retry on a network failure", async () => {
    let first = true;
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(async (input: string) => {
        if (first) {
          first = false;
          return new Response("boom", { status: 500 });
        }
        const path = new URL(input, "http://localhost").pathname;
        const routes: Record<string, unknown> = {
          "/applications/app-1": application,
          "/applications/app-1/form-definition": form,
          "/public/competitions/c1": competition,
          "/applications/app-1/attachments": [],
        };
        return new Response(JSON.stringify(routes[path]), { status: 200 });
      }),
    );

    render(<ApplicationPage />);

    await screen.findByText(/Nie udało się pobrać wniosku/);
    fireEvent.click(screen.getByRole("button", { name: "Spróbuj ponownie" }));

    expect(await screen.findByText("tryb wypełniania")).toBeDefined();
  });
});
