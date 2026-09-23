import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  createCompetition,
  fetchOperatorCompetition,
  fetchOperatorCompetitions,
  fetchOperators,
  publishCompetition,
  updateCompetition,
} from "./operator-competitions";

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
});

describe("fetchOperatorCompetitions", () => {
  it("reads the operator route, not the public one", async () => {
    fetchMock.mockResolvedValue(answer(200, []));

    await fetchOperatorCompetitions();

    expect(fetchMock.mock.calls[0][0]).toMatch(/\/competitions$/);
  });
});

describe("fetchOperatorCompetition", () => {
  it("puts the identifier into the address", async () => {
    fetchMock.mockResolvedValue(answer(200, { id: "abc" }));

    await fetchOperatorCompetition("5b2a0f06-1d3c-4a1e-9f1a-1f3f2b7c9d01");

    expect(fetchMock.mock.calls[0][0]).toMatch(
      /\/competitions\/5b2a0f06-1d3c-4a1e-9f1a-1f3f2b7c9d01$/,
    );
  });
});

describe("createCompetition", () => {
  it("posts the request body as is", async () => {
    fetchMock.mockResolvedValue(answer(201, { id: "new" }));

    await createCompetition({
      number: "1/2026",
      title: "Konkurs",
    } as never);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toMatch(/\/competitions$/);
    expect(init).toMatchObject({ method: "POST" });
    expect(JSON.parse(init.body)).toMatchObject({
      number: "1/2026",
      title: "Konkurs",
    });
  });
});

describe("updateCompetition", () => {
  it("puts to the competition's own address", async () => {
    fetchMock.mockResolvedValue(answer(200, { id: "abc" }));

    await updateCompetition("abc", { number: "1/2026" } as never);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toMatch(/\/competitions\/abc$/);
    expect(init).toMatchObject({ method: "PUT" });
  });
});

describe("publishCompetition", () => {
  it("asks for the Published state and nothing else", async () => {
    fetchMock.mockResolvedValue(answer(200, { id: "abc", status: "Published" }));

    await publishCompetition("abc");

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toMatch(/\/competitions\/abc\/status$/);
    expect(JSON.parse(init.body)).toEqual({ status: "Published" });
  });
});

describe("fetchOperators", () => {
  it("reads the staff directory", async () => {
    fetchMock.mockResolvedValue(answer(200, []));

    await fetchOperators();

    expect(fetchMock.mock.calls[0][0]).toMatch(/\/accounts\/operators$/);
  });
});
