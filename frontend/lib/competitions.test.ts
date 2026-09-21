import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  competitionPath,
  fetchPublicCompetition,
  fetchPublicCompetitions,
} from "./competitions";

function answer(status: number, body: unknown = null) {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers(),
    text: async () => (body === null ? "" : JSON.stringify(body)),
  } as unknown as Response;
}

const fetchMock = vi.fn();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
});

describe("fetchPublicCompetitions", () => {
  it("asks the API the frontend container can actually reach", async () => {
    // The browser address would be this container itself on the server side,
    // and nothing answers on it.
    vi.stubEnv("API_SERVER_URL", "http://backend:8080");
    fetchMock.mockResolvedValue(answer(200, []));

    await fetchPublicCompetitions();

    expect(fetchMock.mock.calls[0][0]).toBe(
      "http://backend:8080/public/competitions",
    );
  });

  it("never reads a cached copy, because only the clock moves the intake", async () => {
    fetchMock.mockResolvedValue(answer(200, []));

    await fetchPublicCompetitions();

    expect(fetchMock.mock.calls[0][1]).toMatchObject({ cache: "no-store" });
  });
});

describe("fetchPublicCompetition", () => {
  it("puts the identifier into the address instead of sending the template", async () => {
    fetchMock.mockResolvedValue(answer(200, { id: "abc" }));

    await fetchPublicCompetition("5b2a0f06-1d3c-4a1e-9f1a-1f3f2b7c9d01");

    expect(fetchMock.mock.calls[0][0]).toMatch(
      /\/public\/competitions\/5b2a0f06-1d3c-4a1e-9f1a-1f3f2b7c9d01$/,
    );
  });

  it("escapes whatever arrived in the address bar", async () => {
    fetchMock.mockResolvedValue(answer(404));

    await fetchPublicCompetition("../competitions?x=1");

    // Whatever was typed stays one path segment: no slash to climb out of the
    // route with, no question mark to grow a query string on.
    const segment = String(fetchMock.mock.calls[0][0]).split(
      "/public/competitions/",
    )[1];
    expect(segment).not.toMatch(/[/?]/);
  });

  it("turns the 404 of a draft into an absence, not an error", async () => {
    fetchMock.mockResolvedValue(answer(404));

    await expect(fetchPublicCompetition("roboczy")).resolves.toBeNull();
  });

  it("lets a broken backend stay broken instead of looking like a missing page", async () => {
    fetchMock.mockResolvedValue(answer(500));

    await expect(fetchPublicCompetition("cokolwiek")).rejects.toThrow();
  });
});

describe("competitionPath", () => {
  it("is the one place that spells the public address of a competition", () => {
    expect(competitionPath("abc")).toBe("/competitions/abc");
  });
});
