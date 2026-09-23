import { describe, expect, it } from "vitest";

import { stepForField, stepsForFields } from "./field-steps";

describe("stepForField", () => {
  it("maps a flat field to its step", () => {
    expect(stepForField("number")).toBe("basics");
    expect(stepForField("description")).toBe("description");
    expect(stepForField("requiresPaperSubmission")).toBe("paper");
    expect(stepForField("maxGrantAmount")).toBe("limits");
    expect(stepForField("maxAttachmentSizeInBytes")).toBe("attachments");
    expect(stepForField("contactUserIds")).toBe("contacts");
  });

  it("maps an indexed attachment field to the attachments step", () => {
    expect(stepForField("attachments[0].title")).toBe("attachments");
    expect(stepForField("attachments[3].allowedFormats")).toBe("attachments");
  });

  it("falls back to the summary step for an unknown field", () => {
    expect(stepForField("somethingNew")).toBe("summary");
  });
});

describe("stepsForFields", () => {
  it("deduplicates and covers every field's step", () => {
    expect(
      stepsForFields(["number", "title", "maxGrantAmount"]),
    ).toEqual(["basics", "limits"]);
  });
});
