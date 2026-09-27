import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import { ApplyLink } from "./apply-link";

const openIntake = {
  acceptsApplications: true,
  state: "Open",
  opensAt: "2026-09-01T08:00:00Z",
  closesAt: "2026-10-25T10:00:00Z",
  message: "Nabór trwa.",
} as const;

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(new Response(body === null ? null : JSON.stringify(body), { status })),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ApplyLink", () => {
  it("sends an anonymous visitor through sign in, unchanged, while GET /me is still in flight", () => {
    respondWith(null, 401);

    render(<ApplyLink competitionId="c1" intake={openIntake} />);

    const link = screen.getByRole("link", { name: "Wypełnij wniosek" });
    expect(link.getAttribute("href")).toBe(
      `/login?returnUrl=${encodeURIComponent("/competitions/c1")}`,
    );
  });

  it("keeps sending a signed in operator through sign in: this button is for applicants", async () => {
    respondWith({
      id: "u1",
      email: "biuro@ocwip.pl",
      firstName: "Jan",
      lastName: "Operator",
      role: "Operator",
      entityName: null,
    });

    render(<ApplyLink competitionId="c1" intake={openIntake} />);

    expect(await screen.findByRole("link", { name: "Wypełnij wniosek" })).toBeDefined();
  });

  it("leads a signed in applicant to the step with the applicant's data, not straight to a draft", async () => {
    // The draft is started there (T-93): at the first application that step
    // creates the Podmiot card the draft belongs to.
    respondWith({
      id: "u1",
      email: "biuro@example.org",
      firstName: "Ada",
      lastName: "Testowa",
      role: "Applicant",
      entityName: null,
    });

    render(<ApplyLink competitionId="c1" intake={openIntake} />);

    await vi.waitFor(() =>
      expect(screen.getByRole("link", { name: "Wypełnij wniosek" }).getAttribute("href")).toBe(
        "/panel/applicant/start/c1",
      ),
    );
  });

  it("offers no button at all once the intake is shut", () => {
    respondWith(null, 401);

    render(
      <ApplyLink
        competitionId="c1"
        intake={{ ...openIntake, acceptsApplications: false, message: "Nabór został zamknięty." }}
      />,
    );

    expect(screen.queryByRole("link", { name: "Wypełnij wniosek" })).toBeNull();
    expect(screen.getByText("Wniosku nie da się teraz rozpocząć.")).toBeDefined();
  });
});
