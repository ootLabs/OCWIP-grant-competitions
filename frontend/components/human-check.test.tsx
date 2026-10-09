import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { LoginForm } from "@/app/(account)/login/login-form";
import { humanCheckHeader, humanCheckProblemType, type TurnstileRenderOptions } from "@/lib/human-check";

import { HumanCheckProvider } from "./human-check";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: vi.fn() }),
}));

/** Cloudflare's widget, standing in: keeps what it was asked to draw. */
let widgets: TurnstileRenderOptions[];
const remove = vi.fn();

beforeEach(() => {
  widgets = [];
  window.turnstile = {
    render: (_container, options) => {
      widgets.push(options);
      return `widget-${widgets.length}`;
    },
    remove,
  };
});

afterEach(() => {
  cleanup();
  remove.mockReset();
  delete window.turnstile;
  vi.unstubAllGlobals();
});

function renderLogin() {
  render(
    <HumanCheckProvider siteKey="1x00000000000000000000AA">
      <LoginForm returnUrl={null} />
    </HumanCheckProvider>,
  );
}

function fill() {
  fireEvent.change(screen.getByLabelText("Adres e-mail"), { target: { value: "biuro@example.org" } });
  fireEvent.change(screen.getByLabelText("Hasło"), { target: { value: "Haslo123!" } });
}

const button = () => screen.getByRole("button", { name: "Zaloguj się" }) as HTMLButtonElement;

describe("useHumanCheck", () => {
  it("keeps the button off until the widget hands over a token, then sends the token", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ redirectPath: "/panel" }), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);
    renderLogin();
    fill();

    await waitFor(() => expect(widgets).toHaveLength(1));
    expect(widgets[0]).toMatchObject({ sitekey: "1x00000000000000000000AA", action: "login", language: "pl" });
    expect(button().disabled).toBe(true);

    act(() => widgets[0].callback!("token-1"));
    expect(button().disabled).toBe(false);
    fireEvent.click(button());

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(fetchMock.mock.calls[0][1].headers[humanCheckHeader]).toBe("token-1");
  });

  it("draws a new widget after a refusal, because a token is good once", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            status: 400,
            type: humanCheckProblemType,
            detail: "Nie udało się potwierdzić, że formularz wysyła człowiek.",
          }),
          { status: 400, headers: { "content-type": "application/problem+json" } },
        ),
      ),
    );
    renderLogin();
    fill();
    await waitFor(() => expect(widgets).toHaveLength(1));
    act(() => widgets[0].callback!("token-1"));
    fireEvent.click(button());

    // The backend's own sentence, not "wrong password": no credential was read.
    expect(await screen.findByRole("alert")).toHaveProperty(
      "textContent",
      "Nie udało się potwierdzić, że formularz wysyła człowiek.",
    );
    await waitFor(() => expect(widgets).toHaveLength(2));
    expect(remove).toHaveBeenCalledWith("widget-1");
    expect(button().disabled).toBe(true);
  });

  it("turns the button off again when the token expires", async () => {
    renderLogin();
    await waitFor(() => expect(widgets).toHaveLength(1));
    act(() => widgets[0].callback!("token-1"));
    expect(button().disabled).toBe(false);

    act(() => widgets[0]["expired-callback"]!());

    expect(button().disabled).toBe(true);
  });

  it("draws nothing and holds nothing back without a site key", () => {
    render(<LoginForm returnUrl={null} />);

    expect(widgets).toHaveLength(0);
    expect(button().disabled).toBe(false);
  });
});
