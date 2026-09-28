import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import type { FormDocument, FormField } from "@/lib/forms/document-types";
import { FieldEditor } from "./field-editor";

const field: FormField = {
  key: "rachunek_lidera",
  type: "shortText",
  label: "Numer rachunku lidera",
  help: "",
  required: true,
  printed: true,
};

const document: FormDocument = {
  schemaVersion: 1,
  sections: [{ key: "wnioskodawca", title: "Wnioskodawca", description: "", fields: [field] }],
};

afterEach(cleanup);

describe("FieldEditor", () => {
  it("marks a field as a person's data, encrypted in the database (T-47a)", () => {
    const onChange = vi.fn();
    render(
      <FieldEditor
        document={document}
        sectionKey="wnioskodawca"
        fieldKey="rachunek_lidera"
        field={field}
        onChange={onChange}
      />,
    );

    const box = screen.getByLabelText("Dane osobowe (szyfrowane w bazie)");
    expect((box as HTMLInputElement).checked).toBe(false);

    fireEvent.click(box);

    expect(onChange).toHaveBeenCalledWith({ ...field, sensitive: true });
  });
});
