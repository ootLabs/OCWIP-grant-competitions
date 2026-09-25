import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";

import type { PublicCompetition } from "@/lib/competitions";

const fetchPublicCompetitions = vi.fn();
const fetchPublicCompetition = vi.fn();

vi.mock("@/lib/competitions", async (importOriginal) => ({
  // The path helper and the types are the real ones: a test that invents its
  // own address stops noticing the day the real one changes.
  ...(await importOriginal<typeof import("@/lib/competitions")>()),
  fetchPublicCompetitions: () => fetchPublicCompetitions(),
  fetchPublicCompetition: (id: string) => fetchPublicCompetition(id),
}));

// None of this page's own tests are about being signed in: that behaviour
// belongs to app/competitions/apply-link.test.tsx, which mocks GET /me
// itself. Here every visitor is anonymous, the same as before ApplyLink
// learned to ask.
vi.mock("@/lib/session", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/session")>()),
  fetchCurrentUser: () => Promise.resolve(null),
}));

vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn() }) }));

const { default: CompetitionsPage } = await import("./page");
const { default: CompetitionPage, generateMetadata } = await import(
  "./[id]/page"
);

const OPEN_COMPETITION: PublicCompetition = {
  id: "5b2a0f06-1d3c-4a1e-9f1a-1f3f2b7c9d01",
  number: "OIL/2026/01",
  title: "Opolskie Inicjatywy Lokalne 2026",
  description: "Konkurs dla organizacji z regionu.\n\nDrugi akapit opisu.",
  status: "OpenForApplications",
  intake: {
    acceptsApplications: true,
    state: "Open",
    opensAt: "2026-09-01T08:00:00Z",
    closesAt: "2026-10-25T10:00:00Z",
    message: "Nabór trwa. Wnioski można składać do 25.10.2026 o godzinie 11:00 czasu polskiego.",
  },
  startDate: "2026-09-01T08:00:00Z",
  endDate: "2026-10-25T10:00:00Z",
  isContinuousIntake: false,
  maxGrantAmount: 15000,
  expectedResults: "Trzy zrealizowane inicjatywy sąsiedzkie.",
  rulesUrl: "https://ocwip.pl/regulamin",
  requiresPaperSubmission: false,
  paperSubmissionDeadline: null,
  paperSubmissionAddress: null,
  projectStartDate: "2027-01-01",
  projectEndDate: "2027-06-30",
  totalPoolAmount: 120000,
  minGrantAmount: 2000,
  maxIndirectCostPercent: 20,
  maxInstitutionalDevelopmentPercent: null,
  percentageBasis: "GrantAmount",
  maxAverageAnnualRevenue: null,
  personalDataProcessedUntil: "2032-12-31",
  costCategories: ["DirectCosts", "IndirectCosts"],
  maxAttachmentSizeInBytes: 10 * 1024 * 1024,
  maxApplicationSizeInBytes: 50 * 1024 * 1024,
  attachments: [
    {
      id: "a2a0f06d-1d3c-4a1e-9f1a-1f3f2b7c9d02",
      title: "Statut organizacji",
      description: "Aktualny statut podpisany przez zarząd.",
      requirement: "RequiredOutsideKrs",
      allowedFormats: ["Pdf", "Docx"],
    },
  ],
  contacts: [
    {
      userId: "c3a0f06d-1d3c-4a1e-9f1a-1f3f2b7c9d03",
      name: "Anna Kowalska",
      email: "anna.kowalska@ocwip.pl",
    },
  ],
};

function competition(overrides: Partial<PublicCompetition> = {}): PublicCompetition {
  return { ...OPEN_COMPETITION, ...overrides };
}

function closed(): PublicCompetition {
  return competition({
    status: "Closed",
    intake: {
      acceptsApplications: false,
      state: "Closed",
      opensAt: "2026-09-01T08:00:00Z",
      closesAt: "2026-10-25T10:00:00Z",
      message:
        "Nabór został zamknięty 25.10.2026 o godzinie 11:00 czasu polskiego. Wniosku nie można już złożyć.",
    },
  });
}

async function renderList() {
  render(await CompetitionsPage());
}

async function renderCompetition(value: PublicCompetition) {
  fetchPublicCompetition.mockResolvedValue(value);
  render(await CompetitionPage({ params: Promise.resolve({ id: value.id }) }));
}

beforeEach(() => {
  fetchPublicCompetitions.mockReset();
  fetchPublicCompetition.mockReset();
});

afterEach(cleanup);

describe("publiczna lista konkursów", () => {
  it("shows an announced competition without anybody being signed in", async () => {
    fetchPublicCompetitions.mockResolvedValue([competition()]);

    await renderList();

    const link = screen.getByRole("link", {
      name: "Opolskie Inicjatywy Lokalne 2026",
    });
    expect(link.getAttribute("href")).toBe(`/competitions/${OPEN_COMPETITION.id}`);

    // The two things somebody decides on before opening anything: how much
    // money, and by when. The deadline arrives inside the rule's sentence.
    expect(screen.getByText(/15 000,00/)).toBeTruthy();
    expect(
      screen.getByText(/Nabór trwa\. Wnioski można składać do 25\.10\.2026/),
    ).toBeTruthy();
  });

  it("says what is going on when there is no call open", async () => {
    fetchPublicCompetitions.mockResolvedValue([]);

    await renderList();

    expect(screen.getByRole("heading", { level: 2 }).textContent).toMatch(
      /Nie ma teraz ogłoszonego konkursu/,
    );
  });

  it("names a continuous intake instead of leaving the deadline empty", async () => {
    fetchPublicCompetitions.mockResolvedValue([
      competition({
        isContinuousIntake: true,
        endDate: null,
        intake: {
          acceptsApplications: true,
          state: "Open",
          opensAt: "2026-09-01T08:00:00Z",
          closesAt: null,
          message: "Nabór ciągły. Wnioski można składać bez terminu końcowego.",
        },
      }),
    ]);

    await renderList();

    expect(screen.getByText(/Nabór ciągły/)).toBeTruthy();
    expect(screen.queryByText(/Do zamknięcia naboru pozostało/)).toBeNull();
  });
});

describe("strona konkursu", () => {
  it("shows what a guest needs in order to decide whether to apply", async () => {
    await renderCompetition(competition());

    expect(screen.getByRole("heading", { level: 1 }).textContent).toBe(
      "Opolskie Inicjatywy Lokalne 2026",
    );
    expect(screen.getByText(/Drugi akapit opisu/)).toBeTruthy();
    expect(screen.getByText(/Trzy zrealizowane inicjatywy/)).toBeTruthy();
    expect(screen.getByText(/120 000,00/)).toBeTruthy();
    expect(screen.getByText(/Statut organizacji/)).toBeTruthy();
    expect(screen.getByText(/Wymagany od podmiotów spoza KRS/)).toBeTruthy();
    expect(screen.getByText(/anna.kowalska@ocwip.pl/)).toBeTruthy();
    expect(screen.getByRole("link", { name: /Regulamin konkursu/ })).toBeTruthy();
  });

  it("counts the percentage limits off the basis the operator chose", async () => {
    await renderCompetition(
      competition({ percentageBasis: "TotalProjectValue" }),
    );

    expect(
      screen.getByText(/20% całkowitej wartości projektu/),
    ).toBeTruthy();
  });

  it("leaves out a parameter nobody set instead of printing a dash", async () => {
    await renderCompetition(competition({ totalPoolAmount: null }));

    expect(screen.queryByText("Pula konkursu")).toBeNull();
  });

  it("sends the apply button through sign in and back to this competition", async () => {
    await renderCompetition(competition());

    const apply = screen.getByRole("link", { name: "Wypełnij wniosek" });
    expect(apply.getAttribute("href")).toBe(
      `/login?returnUrl=${encodeURIComponent(`/competitions/${OPEN_COMPETITION.id}`)}`,
    );
  });

  it("offers no apply button once the intake is shut, and says when it shut", async () => {
    await renderCompetition(closed());

    expect(screen.queryByRole("link", { name: "Wypełnij wniosek" })).toBeNull();
    // D12: the sentence carries the moment that decided it, not just a verdict.
    expect(screen.getByText(/Nabór został zamknięty 25\.10\.2026/)).toBeTruthy();
  });

  it("counts down only while there is something to count down to", async () => {
    await renderCompetition(closed());

    expect(screen.queryByText(/Do zamknięcia naboru pozostało/)).toBeNull();
  });

  it("states the deadline once, in the words of the rule that owns it", async () => {
    await renderCompetition(competition());

    expect(
      screen.getAllByText(/Wnioski można składać do 25\.10\.2026/),
    ).toHaveLength(1);
  });

  it("answers 404 for an address that has no competition behind it", async () => {
    fetchPublicCompetition.mockResolvedValue(null);

    await expect(
      CompetitionPage({ params: Promise.resolve({ id: "nie-ma-takiego" }) }),
    ).rejects.toThrow();
  });

  it("keeps the heading structure walkable, with no level skipped", async () => {
    const { container } = await renderCompetitionAndReturn(competition());

    const levels = [...container.querySelectorAll("h1, h2, h3")].map((heading) =>
      Number(heading.tagName.slice(1)),
    );

    expect(levels[0]).toBe(1);
    levels.forEach((level, index) => {
      if (index > 0) {
        expect(level - levels[index - 1]).toBeLessThanOrEqual(1);
      }
    });
  });

  it("gives every attachment its own list entry", async () => {
    await renderCompetition(competition());

    const attachments = screen.getByRole("heading", {
      name: "Wymagane załączniki",
    }).parentElement as HTMLElement;

    expect(within(attachments).getAllByRole("listitem")).toHaveLength(1);
  });
});

describe("podgląd wklejonego odnośnika", () => {
  it("puts the title, the amount and the deadline into the Open Graph tags", async () => {
    fetchPublicCompetition.mockResolvedValue(competition());

    const metadata = await generateMetadata({
      params: Promise.resolve({ id: OPEN_COMPETITION.id }),
    });

    expect(metadata.openGraph?.title).toBe("Opolskie Inicjatywy Lokalne 2026");
    expect(metadata.openGraph?.description).toMatch(/15\s000,00/);
    expect(metadata.openGraph?.description).toMatch(/25\.10\.2026, 11:00/);
    expect(metadata.openGraph?.description).toMatch(/czasu polskiego/);
  });

  it("puts nothing about a competition that has no public address", async () => {
    fetchPublicCompetition.mockResolvedValue(null);

    const metadata = await generateMetadata({
      params: Promise.resolve({ id: "roboczy" }),
    });

    expect(metadata.openGraph).toBeUndefined();
    expect(metadata.title).not.toMatch(/Opolskie/);
  });
});

async function renderCompetitionAndReturn(value: PublicCompetition) {
  fetchPublicCompetition.mockResolvedValue(value);

  return render(
    await CompetitionPage({ params: Promise.resolve({ id: value.id }) }),
  );
}
