import { afterEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";

import { SettingsForm } from "./settings-form";

const settings = {
  evaluatorsPerApplication: 2,
  scoreAggregation: "Sum" as const,
  meritThreshold: 50,
  thresholdIncludesStrategic: false,
  divergenceThresholdPercent: 30,
};

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("SettingsForm", () => {
  it("sends an emptied field as no value, not as zero", async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(
        async (_input: string, init: RequestInit) =>
          new Response(init.body as string, { status: 200 }),
      );
    vi.stubGlobal("fetch", fetchMock);
    const onSaved = vi.fn();

    render(
      <SettingsForm competitionId="c1" settings={settings} onSaved={onSaved} />,
    );

    fireEvent.change(screen.getByLabelText(/Próg rozbieżności/), {
      target: { value: "" },
    });
    fireEvent.change(screen.getByLabelText("Wynik wniosku z kart ekspertów"), {
      target: { value: "Average" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Zapisz ustawienia" }));

    await screen.findByText("Zapisano ustawienia oceny.");
    const [input, init] = fetchMock.mock.calls[0];
    expect(String(input)).toContain("/competitions/c1/evaluation-settings");
    expect(init.method).toBe("PUT");
    expect(JSON.parse(init.body as string)).toEqual({
      evaluatorsPerApplication: 2,
      scoreAggregation: "Average",
      meritThreshold: 50,
      thresholdIncludesStrategic: false,
      divergenceThresholdPercent: null,
    });
    await waitFor(() => expect(onSaved).toHaveBeenCalled());
  });

  it("says why the settings were not saved", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({ title: "Liczba ekspertów musi być od 1 do 20." }),
          {
            status: 400,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );

    render(
      <SettingsForm competitionId="c1" settings={settings} onSaved={vi.fn()} />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Zapisz ustawienia" }));

    expect((await screen.findByRole("status")).textContent).not.toBe(
      "Zapisano ustawienia oceny.",
    );
  });
});
