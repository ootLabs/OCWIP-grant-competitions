import { describe, expect, it } from "vitest";

import { newDraft } from "./draft-factory";
import { toCompetitionRequest } from "./to-request";

describe("toCompetitionRequest", () => {
  it("refuses to build a request when the structurally required fields are missing", () => {
    const { request, structuralGaps } = toCompetitionRequest(newDraft());

    expect(request).toBeNull();
    expect(structuralGaps).toContain("Numer konkursu");
    expect(structuralGaps).toContain("Tytuł konkursu");
    expect(structuralGaps).toContain("Rozpoczęcie naboru wniosków");
    expect(structuralGaps).toContain(
      "Zakończenie naboru wniosków (albo zaznacz nabór ciągły)",
    );
    expect(structuralGaps).toContain("Maksymalna dotacja na jeden wniosek");
  });

  it("does not ask for an end date when the intake is continuous", () => {
    const draft = {
      ...newDraft(),
      number: "1/2026",
      title: "Konkurs",
      startDateLocal: "2026-09-01T08:00",
      isContinuousIntake: true,
      maxGrantAmount: "5000",
    };

    const { request, structuralGaps } = toCompetitionRequest(draft);

    expect(structuralGaps).toEqual([]);
    expect(request?.endDate).toBeNull();
    expect(request?.isContinuousIntake).toBe(true);
  });

  it("converts local readings to UTC instants and keeps amounts as text", () => {
    const draft = {
      ...newDraft(),
      number: "1/2026",
      title: "Konkurs",
      startDateLocal: "2026-09-01T08:00",
      endDateLocal: "2026-09-30T12:00",
      maxGrantAmount: "12345.67",
    };

    const { request } = toCompetitionRequest(draft);

    expect(request?.startDate).toBe("2026-09-01T06:00:00.000Z");
    expect(request?.endDate).toBe("2026-09-30T10:00:00.000Z");
    // A string, not a parsed number: this is the value that keeps the last
    // grosz instead of losing it to floating point.
    expect(request?.maxGrantAmount).toBe("12345.67");
  });

  it("turns blank optional text into null instead of an empty string", () => {
    const draft = {
      ...newDraft(),
      number: "1/2026",
      title: "Konkurs",
      startDateLocal: "2026-09-01T08:00",
      endDateLocal: "2026-09-30T12:00",
      maxGrantAmount: "5000",
      description: "   ",
    };

    const { request } = toCompetitionRequest(draft);

    expect(request?.description).toBeNull();
  });

  it("drops the paper submission fields when the requirement is off", () => {
    const draft = {
      ...newDraft(),
      number: "1/2026",
      title: "Konkurs",
      startDateLocal: "2026-09-01T08:00",
      endDateLocal: "2026-09-30T12:00",
      maxGrantAmount: "5000",
      requiresPaperSubmission: false,
      paperSubmissionDeadlineLocal: "2026-09-25T12:00",
      paperSubmissionAddress: "ul. Testowa 1",
    };

    const { request } = toCompetitionRequest(draft);

    expect(request?.paperSubmissionDeadline).toBeNull();
    expect(request?.paperSubmissionAddress).toBeNull();
  });

  it("keeps the paper submission fields when the requirement is on", () => {
    const draft = {
      ...newDraft(),
      number: "1/2026",
      title: "Konkurs",
      startDateLocal: "2026-09-01T08:00",
      endDateLocal: "2026-09-30T12:00",
      maxGrantAmount: "5000",
      requiresPaperSubmission: true,
      paperSubmissionDeadlineLocal: "2026-09-25T12:00",
      paperSubmissionAddress: "ul. Testowa 1",
    };

    const { request } = toCompetitionRequest(draft);

    expect(request?.paperSubmissionDeadline).toBe("2026-09-25T10:00:00.000Z");
    expect(request?.paperSubmissionAddress).toBe("ul. Testowa 1");
  });

  it("converts megabytes to bytes for the upload limits", () => {
    const draft = {
      ...newDraft(),
      number: "1/2026",
      title: "Konkurs",
      startDateLocal: "2026-09-01T08:00",
      endDateLocal: "2026-09-30T12:00",
      maxGrantAmount: "5000",
      maxAttachmentSizeInMegabytes: "10",
      maxApplicationSizeInMegabytes: "50",
    };

    const { request } = toCompetitionRequest(draft);

    expect(request?.maxAttachmentSizeInBytes).toBe(10 * 1024 * 1024);
    expect(request?.maxApplicationSizeInBytes).toBe(50 * 1024 * 1024);
  });

  it("carries attachments and contact ids through", () => {
    const draft = {
      ...newDraft(),
      number: "1/2026",
      title: "Konkurs",
      startDateLocal: "2026-09-01T08:00",
      endDateLocal: "2026-09-30T12:00",
      maxGrantAmount: "5000",
      attachments: [
        {
          title: "Odpis z rejestru",
          description: "",
          requirement: "Required" as const,
          allowedFormats: ["Pdf" as const],
        },
      ],
      contactUserIds: ["11111111-1111-1111-1111-111111111111"],
    };

    const { request } = toCompetitionRequest(draft);

    expect(request?.attachments).toEqual([
      {
        title: "Odpis z rejestru",
        description: null,
        requirement: "Required",
        allowedFormats: ["Pdf"],
      },
    ]);
    expect(request?.contactUserIds).toEqual([
      "11111111-1111-1111-1111-111111111111",
    ]);
  });
});
