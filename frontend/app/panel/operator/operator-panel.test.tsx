import { afterEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { OperatorPanel } from "./operator-panel";

const replace = vi.fn();
let pathname = "/panel/operator";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
  usePathname: () => pathname,
}));

const operator = {
  id: "7f6c2e30-0000-4000-8000-000000000002",
  email: "biuro@ocwip.pl",
  firstName: "Ewa",
  lastName: "Operatorska",
  role: "Operator",
  entityName: null,
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
  pathname = "/panel/operator";
  window.history.replaceState({}, "", "/panel/operator");
  vi.unstubAllGlobals();
});

describe("OperatorPanel", () => {
  it("carries the whole navigation of the card", async () => {
    respondWith(operator);

    render(
      <OperatorPanel>
        <p>Treść panelu</p>
      </OperatorPanel>,
    );

    const navigation = await screen.findByRole("navigation", {
      name: "Panel operatora",
    });

    expect(
      Array.from(navigation.querySelectorAll("a")).map((link) => link.textContent),
    ).toEqual(["Konkursy", "Wnioski", "Formularze", "Recenzenci"]);
  });

  it("says on every screen that this is the operator's view of other people's data", async () => {
    // The operator reads personal data belonging to other organisations, often
    // with somebody from outside watching the same screen. There must be no
    // moment in which "whose view is this" needs a click to answer.
    respondWith(operator);

    render(
      <OperatorPanel>
        <p>Treść panelu</p>
      </OperatorPanel>,
    );

    expect(
      await screen.findByText(
        "Tryb operatora. Widzisz dane wszystkich podmiotów, nie własne.",
      ),
    ).toBeDefined();
    expect(screen.getByText("Zalogowano jako Ewa Operatorska")).toBeDefined();
  });

  it("puts the mode marking before anything else a keyboard or a reader meets", async () => {
    respondWith(operator);

    const { container } = render(
      <OperatorPanel>
        <p>Treść panelu</p>
      </OperatorPanel>,
    );

    await screen.findByText("Zalogowano jako Ewa Operatorska");

    const header = container.querySelector("header");
    expect(header?.firstElementChild?.textContent).toBe(
      "Tryb operatora. Widzisz dane wszystkich podmiotów, nie własne.",
    );

    // Order in the DOM is order under Tab. The skip link only works if it is
    // reached before the thing it skips.
    const focusable = Array.from(container.querySelectorAll("a, button")).map(
      (element) => element.textContent,
    );
    expect(focusable[0]).toBe("Przejdź do treści");
    expect(container.querySelector("#tresc")).not.toBeNull();
  });

  it("refuses an applicant with a 403 instead of asking them to log in again", async () => {
    // Their session is valid, it is simply not an operator's, so a redirect to
    // the login screen would promise that logging in again would help.
    respondWith({
      ...operator,
      role: "Applicant",
      entityName: "Fundacja Testowa",
    });

    render(
      <OperatorPanel>
        <p>Treść panelu</p>
      </OperatorPanel>,
    );

    expect(
      await screen.findByText("403. Nie masz dostępu do panelu operatora"),
    ).toBeDefined();
    expect(screen.queryByText("Treść panelu")).toBeNull();
    expect(screen.queryByText(/Tryb operatora/)).toBeNull();
    expect(replace).not.toHaveBeenCalled();
  });

  it("refuses a reviewer too, because the rule is one allowed role and not a list of refused ones", async () => {
    respondWith({ ...operator, role: "Reviewer" });

    render(
      <OperatorPanel>
        <p>Treść panelu</p>
      </OperatorPanel>,
    );

    expect(
      await screen.findByText("403. Nie masz dostępu do panelu operatora"),
    ).toBeDefined();
    expect(screen.queryByText("Treść panelu")).toBeNull();
  });

  it("does not render the frame while the session is still unknown", async () => {
    respondWith("", 401);

    render(
      <OperatorPanel>
        <p>Treść panelu</p>
      </OperatorPanel>,
    );

    // The frame must never flash: it announces access to everybody's data.
    expect(screen.queryByText(/Tryb operatora/)).toBeNull();

    await waitFor(() => expect(replace).toHaveBeenCalled());
    expect(screen.queryByText(/Tryb operatora/)).toBeNull();
  });

  it("sends a caller without a session to the login screen, remembering where they were", async () => {
    pathname = "/panel/operator/applications";
    window.history.replaceState({}, "", "/panel/operator/applications?konkurs=7");
    respondWith("", 401);

    render(
      <OperatorPanel>
        <p>Treść panelu</p>
      </OperatorPanel>,
    );

    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith(
        "/login?returnUrl=%2Fpanel%2Foperator%2Fapplications%3Fkonkurs%3D7",
      ),
    );
  });

  it("ends the session on the server before leaving the panel", async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(
        async () => new Response(JSON.stringify(operator), { status: 200 }),
      );
    vi.stubGlobal("fetch", fetchMock);

    render(
      <OperatorPanel>
        <p>Treść panelu</p>
      </OperatorPanel>,
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

  it("treats an unreachable backend as an outage, not as an expired session", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("offline")));

    render(
      <OperatorPanel>
        <p>Treść panelu</p>
      </OperatorPanel>,
    );

    expect(
      await screen.findByText("Nie możemy teraz połączyć się z systemem"),
    ).toBeDefined();
    expect(replace).not.toHaveBeenCalled();
  });

  it("holds a table of 120 applications without the frame giving way", async () => {
    // 120 is the number of offers one competition brings in, and the card asks
    // for this to be checked on data rather than on an empty table: the moment
    // the operator needs the list most is right after a call closes.
    respondWith(operator);

    const { container } = render(
      <OperatorPanel>
        <table>
          <tbody>
            {Array.from({ length: 120 }, (_, row) => (
              <tr key={row}>
                {Array.from({ length: 8 }, (_, column) => (
                  <td key={column}>Wiersz {row + 1} kolumna {column + 1}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </OperatorPanel>,
    );

    const content = await waitFor(() => {
      const main = container.querySelector("#tresc");
      expect(main).not.toBeNull();
      return main as HTMLElement;
    });

    expect(within(content).getAllByRole("row")).toHaveLength(120);

    // A flex child defaults to min-width:auto, so a table wider than the
    // viewport would stretch this column and drag the header, mode marking
    // included, off the left edge. These two classes are what keeps the
    // overflow inside the content region, so losing either one is a
    // regression that no rendered row count would catch.
    expect(content.className).toContain("min-w-0");
    expect(content.className).toContain("overflow-x-auto");

    // And the marking has to still be on screen at the bottom of those rows.
    expect(container.querySelector("header")?.className).toContain("sticky");
  });
});
