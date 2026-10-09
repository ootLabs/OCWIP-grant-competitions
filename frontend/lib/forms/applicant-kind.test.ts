import { describe, expect, it } from "vitest";

import { applicantKindGap, prefilledApplicantKind } from "./applicant-kind";
import type { FormDocument } from "./document-types";

const document: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "czesc_1",
      title: "Część I",
      description: "",
      fields: [
        {
          key: "rodzaj",
          type: "singleChoice",
          label: "Wniosek składa",
          help: "",
          required: true,
          printed: true,
          role: "applicantType",
          options: [
            { value: "Organisation", label: "A. organizacja" },
            { value: "PatronInformalGroup", label: "B. grupa z patronem" },
            { value: "InformalGroup", label: "C. grupa nieformalna" },
          ],
        },
      ],
    },
  ],
};

describe("applicant kind against the card (O-10)", () => {
  it("fills the kind in for a group without a patron, the only answer that fits", () => {
    expect(prefilledApplicantKind(document, {}, "InformalGroup")).toEqual({ rodzaj: "InformalGroup" });
  });

  it("leaves the choice to an organisation, and never overwrites an answer", () => {
    expect(prefilledApplicantKind(document, {}, "Organisation")).toBeNull();
    expect(prefilledApplicantKind(document, { rodzaj: "Organisation" }, "InformalGroup")).toBeNull();
  });

  it("names an answer that does not fit the card, at its field", () => {
    const gap = applicantKindGap(document, { rodzaj: "Organisation" }, "InformalGroup");

    expect(gap?.fieldKey).toBe("rodzaj");
    expect(gap?.message).toMatch(/grupę nieformalną bez patrona/);
    expect(applicantKindGap(document, { rodzaj: "InformalGroup" }, "Organisation")?.message).toMatch(/opisują organizację/);
  });

  it("says nothing when the answer fits or the card is unknown", () => {
    expect(applicantKindGap(document, { rodzaj: "PatronInformalGroup" }, "Organisation")).toBeNull();
    expect(applicantKindGap(document, { rodzaj: "Organisation" }, null)).toBeNull();
  });
});
