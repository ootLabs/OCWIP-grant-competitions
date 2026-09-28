import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";

import { ReviewerHeader } from "./reviewer-header";
import { reviewerPanelLinks } from "./navigation";

vi.mock("next/navigation", () => ({ usePathname: () => "/panel/reviewer" }));

afterEach(cleanup);

const reviewer = {
  id: "7f6c2e30-0000-4000-8000-000000000002",
  email: "ekspert@example.org",
  firstName: "Jan",
  lastName: "Testowy",
  role: "Reviewer",
  entityName: null,
};

describe("ReviewerHeader", () => {
  it("carries the whole navigation of the panel, including the account placeholder (T-122x)", () => {
    render(<ReviewerHeader user={reviewer} onLogout={vi.fn()} loggingOut={false} />);

    const navigation = screen.getByRole("navigation", { name: "Panel recenzenta" });

    expect(
      Array.from(within(navigation).getAllByRole("link")).map((link) => link.textContent),
    ).toEqual(reviewerPanelLinks.map((link) => link.label));
  });
});
