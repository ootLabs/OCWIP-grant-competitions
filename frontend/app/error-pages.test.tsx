import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import ErrorPage from "./error";
import GlobalError from "./global-error";
import NotFound from "./not-found";

afterEach(cleanup);

/**
 * The card asks for 403, 404 and 500 with a look of their own and a way back.
 * The 403 is not here: it has no route, because the front end is not where
 * access is refused. It is shown where a refusal happens, by the panel gate,
 * and it is tested in the panel tests.
 */
describe("strony błędów", () => {
  it("gives 404 a way back that works for somebody who is not signed in", () => {
    render(<NotFound />);

    screen.getByRole("heading", { name: "Nie ma takiej strony" });
    expect(
      screen.getByRole("link", { name: "Wróć na stronę główną" }).getAttribute("href"),
    ).toBe("/");
  });

  it("keeps the failure itself off the 500", () => {
    // A stack trace helps neither an applicant nor the operator, and it maps
    // the inside of an application holding personal data for anybody who can
    // make it fail. The digest is left out for the same reason: it is a number
    // that means nothing on this side of the screen.
    const failure = Object.assign(
      new Error("Npgsql.PostgresException: relation \"applications\" does not exist"),
      { digest: "3141592653", stack: "at ApplicationService.list (Services/list.ts:12)" },
    );

    const { container } = render(<ErrorPage error={failure} reset={() => {}} />);

    expect(container.textContent).not.toMatch(/Npgsql|relation|3141592653|Services\//);
    screen.getByRole("heading", { name: "Coś poszło nie tak" });
  });

  it("retries in place rather than only sending people home", () => {
    // Most of what lands here is one failed request, and it is over by the next
    // attempt. Sending somebody filling in an application back to the start
    // page for that would cost them the screen they were on.
    const reset = vi.fn();

    render(<ErrorPage error={new Error("cokolwiek")} reset={reset} />);

    fireEvent.click(screen.getByRole("button", { name: "Spróbuj ponownie" }));
    expect(reset).toHaveBeenCalledOnce();
  });

  it("covers a broken root layout with the same screen and the same way out", () => {
    // This one replaces the root layout, so it carries its own html, body and
    // stylesheet. React drops those tags when the component is mounted inside
    // another element, so what is checked here is the part that can be: the
    // person gets the same Polish screen and a button that retries, instead of
    // the framework's own English page.
    const reset = vi.fn();

    const { container } = render(<GlobalError error={new Error("layout")} reset={reset} />);

    screen.getByRole("heading", { name: "Coś poszło nie tak" });
    expect(container.textContent).not.toMatch(/layout/);

    fireEvent.click(screen.getByRole("button", { name: "Spróbuj ponownie" }));
    expect(reset).toHaveBeenCalledOnce();
  });
});
