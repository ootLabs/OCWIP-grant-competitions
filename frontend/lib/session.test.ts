import { afterEach, describe, expect, it, vi } from "vitest";
import { accountLabel, fetchCurrentUser, logout } from "./session";

const applicant = {
  id: "7f6c2e30-0000-4000-8000-000000000001",
  email: "biuro@example.org",
  firstName: "Ada",
  lastName: "Testowa",
  role: "Applicant" as const,
  entityName: "Fundacja Testowa",
};

describe("fetchCurrentUser", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("answers with the account behind a live session", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify(applicant), { status: 200 }),
      ),
    );

    expect(await fetchCurrentUser()).toMatchObject({ entityName: "Fundacja Testowa" });
  });

  it("reads 401 as nobody signed in rather than as a failure", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response("", { status: 401 })),
    );

    expect(await fetchCurrentUser()).toBeNull();
  });

  it("lets every other failure through, so a dead backend is not a logout", async () => {
    // The whole point of the distinction: 500 answered as null would send a
    // person with a perfectly good session to the login screen.
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response("", { status: 500 })),
    );

    await expect(fetchCurrentUser()).rejects.toThrow();
  });
});

describe("logout", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("ends the session on the server", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response("", { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    await logout();

    expect(fetchMock.mock.calls[0][0]).toContain("/logout");
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: "POST" });
  });

  it("does not throw when the request fails, so the screen can still be left", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("offline")));

    await expect(logout()).resolves.toBeUndefined();
  });
});

describe("accountLabel", () => {
  it("names the entity an applicant files as", () => {
    expect(accountLabel(applicant)).toBe("Fundacja Testowa");
  });

  it("falls back to the person for an account without an entity", () => {
    expect(
      accountLabel({ ...applicant, role: "Operator", entityName: null }),
    ).toBe("Ada Testowa");
  });
});
