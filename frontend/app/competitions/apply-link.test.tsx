import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

const push = vi.fn();

vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

const createDraft = vi.fn();
vi.mock("@/lib/applicant-applications", () => ({
  createDraft: (...args: unknown[]) => createDraft(...args),
}));

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
  push.mockReset();
  createDraft.mockReset();
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

  it("starts a draft and goes straight to it for a signed in applicant", async () => {
    respondWith({
      id: "u1",
      email: "biuro@example.org",
      firstName: "Ada",
      lastName: "Testowa",
      role: "Applicant",
      entityName: "Fundacja Testowa",
    });
    createDraft.mockResolvedValue({ id: "app-1" });

    render(<ApplyLink competitionId="c1" intake={openIntake} />);

    const button = await screen.findByRole("button", { name: "Wypełnij wniosek" });
    fireEvent.click(button);

    expect(createDraft).toHaveBeenCalledWith("c1");
    await vi.waitFor(() => expect(push).toHaveBeenCalledWith("/panel/applicant/applications/app-1"));
  });

  it("says starting the draft failed rather than leaving the click silent", async () => {
    respondWith({
      id: "u1",
      email: "biuro@example.org",
      firstName: "Ada",
      lastName: "Testowa",
      role: "Applicant",
      entityName: "Fundacja Testowa",
    });
    createDraft.mockRejectedValue(new Error("boom"));

    render(<ApplyLink competitionId="c1" intake={openIntake} />);

    fireEvent.click(await screen.findByRole("button", { name: "Wypełnij wniosek" }));

    expect(await screen.findByRole("alert")).toBeDefined();
    expect(push).not.toHaveBeenCalled();
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
