import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import type { FormDocument } from "@/lib/forms/document-types";

import { FormRenderer } from "./form-renderer";

const document: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "s",
      title: "Sprawozdanie",
      description: "",
      fields: [
        { key: "tytul", type: "shortText", label: "Tytuł z wniosku", help: "", required: false, printed: true, maxLength: 200, readOnly: true, prefillFrom: "opis" },
        {
          key: "budzet",
          type: "repeatableTable",
          label: "Budżet",
          help: "",
          required: true,
          printed: true,
          prefillFrom: "budzet_a",
          table: {
            columns: [
              { key: "pozycja", type: "shortText", label: "Pozycja", help: "", required: false, printed: true, maxLength: 200, readOnly: true, prefillFrom: "nazwa" },
              { key: "wykonana", type: "amount", label: "Wykonana", help: "", required: true, printed: true, minValue: 0 },
            ],
          },
        },
      ],
    },
  ],
} as unknown as FormDocument;

afterEach(cleanup);

describe("FormRenderer with values from the application (T-50a)", () => {
  it("shows a read only field and a read only cell as text, never as an input", () => {
    render(
      <FormRenderer
        document={document}
        initialAnswers={{ tytul: "Ławki w parku", budzet: [{ pozycja: "Deski", wykonana: 1400 }] }}
        competitionSettings={{}}
        onChange={() => undefined}
      />,
    );

    expect(screen.getByText("Ławki w parku")).toBeDefined();
    expect(screen.queryByLabelText(/Tytuł z wniosku/)).toBeNull();
    expect(screen.getByText("Deski")).toBeDefined();
    expect(screen.queryByLabelText(/Pozycja, wiersz 1/)).toBeNull();
    expect(screen.getByLabelText(/Wykonana.*wiersz 1/)).toBeDefined();
  });
});
