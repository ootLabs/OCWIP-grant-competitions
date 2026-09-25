import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";

import ApplicantCompetitions from "./applicant/competitions/page";
import ApplicantProfile from "./applicant/profile/page";

/**
 * The card's first acceptance criterion, checked on every screen at once
 * rather than once per file.
 *
 * The client will see this system with nothing in it, and the screens that are
 * empty today are the screens that stay empty between calls for proposals. A
 * new panel screen added later without an empty state has to fail here, which
 * is why this test walks a list of every screen instead of naming one.
 */
const screens = [
  { name: "Aktualne konkursy (wnioskodawca)", Page: ApplicantCompetitions },
  { name: "Mój profil", Page: ApplicantProfile },
  // "Formularze" left this list in T-26 and "Konkursy (operator)" in T-22,
  // for the same reason: both read real competitions instead of standing
  // empty forever, so each has its own loading, error and empty states,
  // covered by operator/forms/page.test.tsx and operator/page.test.tsx.
  // "Wnioski (operator)" left in T-35 the same way, covered by
  // operator/applications/page.test.tsx. "Moje wnioski" left in T-34,
  // covered by applicant/page.test.tsx. "Recenzenci" left in T-41, covered
  // by operator/reviewers/page.test.tsx.
];

afterEach(cleanup);

describe("puste ekrany paneli", () => {
  it.each(screens)("$name says what is missing and what happens next", ({ Page }) => {
    const { container } = render(<Page />);

    // The page still names itself: the empty state explains the absence, it
    // does not replace the title somebody navigated to.
    expect(screen.getByRole("heading", { level: 1 })).toBeTruthy();

    const emptyState = screen.getByRole("heading", { level: 2 }).parentElement;
    expect(emptyState).not.toBeNull();

    // A next step, not only a statement of emptiness. One sentence at minimum,
    // because "Brak danych" is the screen this card exists to remove.
    const hint = within(emptyState as HTMLElement).getByText(/\S/, {
      selector: "p",
    });
    expect(hint.textContent?.length).toBeGreaterThan(40);

    // Nothing technical leaks into a screen the client reads first.
    expect(container.textContent).not.toMatch(/T-\d|TODO|null|undefined/);
  });
});
