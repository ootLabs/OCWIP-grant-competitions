import { describe, expect, it } from "vitest";

import { lockOutside, openReturn, type ApplicationCorrections } from "./application-corrections";
import type { FormDocument } from "./forms/document-types";

const document: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "dane",
      title: "Dane",
      description: "",
      fields: [{ key: "opis", type: "shortText", label: "Opis", help: "", required: true, printed: true, maxLength: 10 }],
    },
    {
      key: "budzet",
      title: "Budżet",
      description: "",
      fields: [
        {
          key: "koszty",
          type: "repeatableTable",
          label: "Koszty",
          help: "",
          required: true,
          printed: true,
          table: {
            columns: [{ key: "nazwa", type: "shortText", label: "Nazwa", help: "", required: true, printed: true, maxLength: 10 }],
          },
        },
      ],
    },
  ],
};

describe("lockOutside", () => {
  it("makes every field and column outside the unlocked sections read only", () => {
    const locked = lockOutside(document, ["dane"]);

    expect(locked.sections[0].fields[0].readOnly).toBeUndefined();
    expect(locked.sections[1].fields[0].readOnly).toBe(true);
    expect(locked.sections[1].fields[0].table?.columns[0].readOnly).toBe(true);
  });
});

describe("openReturn", () => {
  it("is the return without resolvedAt", () => {
    const corrections = {
      applicationId: "a1",
      status: "Returned",
      versions: [],
      history: [],
      returns: [
        { id: "old", sections: ["dane"], unlocksAttachments: false, message: "", deadline: "", returnedAt: "", resolvedAt: "2026-10-01T00:00:00Z" },
        { id: "new", sections: ["dane"], unlocksAttachments: false, message: "", deadline: "", returnedAt: "", resolvedAt: null },
      ],
    } as ApplicationCorrections;

    expect(openReturn(corrections)?.id).toBe("new");
    expect(openReturn({ ...corrections, returns: [corrections.returns[0]] })).toBeNull();
  });
});
