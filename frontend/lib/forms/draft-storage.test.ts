import { beforeEach, describe, expect, it } from "vitest";
import { clearDraft, loadDraft, saveDraft } from "./draft-storage";
import type { FormDocument } from "./document-types";

const document: FormDocument = { schemaVersion: 1, sections: [] };

describe("draft-storage", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it("returns null when nothing was saved", () => {
    expect(loadDraft("competition-1")).toBeNull();
  });

  it("round trips a saved draft", () => {
    saveDraft("competition-1", document, "competition-0");
    const draft = loadDraft("competition-1");

    expect(draft?.document).toEqual(document);
    expect(draft?.copiedFromCompetitionId).toBe("competition-0");
    expect(draft?.savedAt).toEqual(expect.any(String));
  });

  it("keeps drafts of different competitions apart", () => {
    saveDraft("competition-1", document, null);
    expect(loadDraft("competition-2")).toBeNull();
  });

  it("removes a draft on clearDraft", () => {
    saveDraft("competition-1", document, null);
    clearDraft("competition-1");
    expect(loadDraft("competition-1")).toBeNull();
  });

  it("survives a corrupted entry instead of throwing", () => {
    window.localStorage.setItem("ocwip:form-draft:competition-1", "{not json");
    expect(loadDraft("competition-1")).toBeNull();
  });
});
