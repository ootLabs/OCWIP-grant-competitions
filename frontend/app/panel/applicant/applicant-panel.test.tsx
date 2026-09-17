import { afterEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { ApplicantPanel } from "./applicant-panel";

const replace = vi.fn();
let pathname = "/panel/applicant";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
  usePathname: () => pathname,
}));

const applicant = {
  id: "7f6c2e30-0000-4000-8000-000000000001",
  email: "biuro@example.org",
  firstName: "Ada",
  lastName: "Testowa",
  role: "Applicant",
  entityName: "Fundacja Testowa",
};

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(new Response(JSON.stringify(body), { status })),
  );
}

afterEach(() => {
  cleanup();
  replace.mockReset();
  pathname = "/panel/applicant";
  vi.unstubAllGlobals();
});

describe("ApplicantPanel", () => {
  it("names the signed in entity and offers the way out", async () => {
    respondWith(applicant);

    render(
      <ApplicantPanel>
        <p>Treść panelu</p>
      </ApplicantPanel>,
    );

    expect(await screen.findByText("Fundacja Testowa")).toBeDefined();
    expect(screen.getByRole("button", { name: "Wyloguj" })).toBeDefined();
  });

  it("carries the whole navigation of the card", async () => {
    respondWith(applicant);

    render(
      <ApplicantPanel>
        <p>Treść panelu</p>
      </ApplicantPanel>,
    );

    const navigation = await screen.findByRole("navigation", {
      name: "Panel wnioskodawcy",
    });

    expect(
      Array.from(navigation.querySelectorAll("a")).map((link) => link.textContent),
    ).toEqual(["Moje wnioski", "Aktualne konkursy", "Mój profil"]);
  });

  it("puts the skip link before the header, so the keyboard starts at the content", async () => {
    respondWith(applicant);

    const { container } = render(
      <ApplicantPanel>
        <p>Treść panelu</p>
      </ApplicantPanel>,
    );

    await within(container).findByText("Fundacja Testowa");

    // Order in the DOM is order under Tab. The skip link only works if it is
    // reached before the thing it skips.
    const focusable = Array.from(
      container.querySelectorAll("a, button"),
    ).map((element) => element.textContent);

    expect(focusable[0]).toBe("Przejdź do treści");
    expect(container.querySelector("#tresc")).not.toBeNull();
  });

  it("sends a caller without a session to the login screen, remembering where they were", async () => {
    pathname = "/panel/applicant/profile";
    respondWith("", 401);

    render(
      <ApplicantPanel>
        <p>Treść panelu</p>
      </ApplicantPanel>,
    );

    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith(
        "/login?returnUrl=%2Fpanel%2Fapplicant%2Fprofile",
      ),
    );
  });

  it("does not render the panel while the session is still unknown", async () => {
    respondWith("", 401);

    render(
      <ApplicantPanel>
        <p>Treść panelu</p>
      </ApplicantPanel>,
    );

    // The frame must never flash for somebody who is not signed in: it names
    // an entity and offers navigation, and neither belongs to them.
    expect(screen.queryByText("Treść panelu")).toBeNull();

    await waitFor(() => expect(replace).toHaveBeenCalled());
    expect(screen.queryByText("Treść panelu")).toBeNull();
  });

  it("ends the session on the server before leaving the panel", async () => {
    // Clearing the cookie in this browser invalidates nothing, and applicants
    // sign in from library computers (docs/architektura.md). The button has to
    // reach the server.
    const fetchMock = vi
      .fn()
      .mockImplementation(async () =>
        new Response(JSON.stringify(applicant), { status: 200 }),
      );
    vi.stubGlobal("fetch", fetchMock);

    render(
      <ApplicantPanel>
        <p>Treść panelu</p>
      </ApplicantPanel>,
    );

    fireEvent.click(await screen.findByRole("button", { name: "Wyloguj" }));

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([url, init]) =>
            String(url).endsWith("/logout") && init?.method === "POST",
        ),
      ).toBe(true),
    );
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
  });

  it("refuses an operator instead of asking them to log in again", async () => {
    respondWith({ ...applicant, role: "Operator", entityName: null });

    render(
      <ApplicantPanel>
        <p>Treść panelu</p>
      </ApplicantPanel>,
    );

    expect(
      await screen.findByText("Ten panel jest dla wnioskodawców"),
    ).toBeDefined();
    expect(screen.queryByText("Treść panelu")).toBeNull();
    expect(replace).not.toHaveBeenCalled();
  });

  it("treats an unreachable backend as an outage, not as an expired session", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("offline")));

    render(
      <ApplicantPanel>
        <p>Treść panelu</p>
      </ApplicantPanel>,
    );

    expect(
      await screen.findByText("Nie możemy teraz połączyć się z systemem"),
    ).toBeDefined();
    expect(replace).not.toHaveBeenCalled();
  });
});
