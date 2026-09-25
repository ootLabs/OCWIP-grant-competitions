import { afterEach, describe, expect, it, vi } from "vitest";
import { confirmationPdfUrl, fetchApplicationForm } from "./applicant-applications";

describe("confirmationPdfUrl", () => {
  it("points at the API, not at the frontend", () => {
    expect(confirmationPdfUrl("abc")).toMatch(/^http.*\/applications\/abc\/confirmation$/);
  });
});

describe("fetchApplicationForm", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("coerces the OpenAPI number-or-string version into a plain number", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            versionNumber: "2",
            definition: { schemaVersion: 1, sections: [] },
          }),
          { status: 200 },
        ),
      ),
    );

    const form = await fetchApplicationForm("abc");

    expect(form.versionNumber).toBe(2);
    expect(form.document).toEqual({ schemaVersion: 1, sections: [] });
  });
});
