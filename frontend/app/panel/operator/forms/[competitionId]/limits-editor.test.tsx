import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import type { FormDocument, FormLimit } from "@/lib/forms/document-types";
import { LimitsEditor } from "./limits-editor";

const document: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "budzet",
      title: "Budżet",
      description: "",
      fields: [
        {
          key: "dotacja",
          type: "calculated",
          label: "Kwota dotacji",
          help: "",
          required: false,
          printed: true,
          calculation: { kind: "sum", operands: [] },
        },
        {
          key: "suma_c",
          type: "calculated",
          label: "Suma C",
          help: "",
          required: false,
          printed: true,
          calculation: { kind: "sum", operands: [] },
        },
      ],
    },
  ],
};

afterEach(cleanup);

function renderWith(limit: FormLimit) {
  const onChange = vi.fn();
  render(
    <LimitsEditor
      document={document}
      sectionKey="budzet"
      fieldKey="suma_c"
      limits={[limit]}
      onChange={onChange}
    />,
  );
  return onChange;
}

describe("LimitsEditor", () => {
  it("takes the percentage from a competition threshold instead of a typed number", () => {
    const onChange = renderWith({ kind: "maxPercentOf", basis: "dotacja", percent: 10 });

    fireEvent.change(screen.getByLabelText("Skąd procent"), {
      target: { value: "competition.maxIndirectCostPercent" },
    });

    expect(onChange).toHaveBeenCalledWith([
      {
        kind: "maxPercentOf",
        basis: "dotacja",
        percentFrom: "competition.maxIndirectCostPercent",
      },
    ]);
  });

  it("hides the number while the percentage comes from the competition", () => {
    renderWith({
      kind: "maxPercentOf",
      basis: "dotacja",
      percentFrom: "competition.maxIndirectCostPercent",
    });

    expect(screen.queryByLabelText("Procent")).toBeNull();
  });

  it("drops both percentage properties when the limit becomes an amount", () => {
    const onChange = renderWith({
      kind: "maxPercentOf",
      basis: "dotacja",
      percentFrom: "competition.maxIndirectCostPercent",
    });

    fireEvent.change(screen.getByDisplayValue("Nie więcej niż procent"), {
      target: { value: "maxAmount" },
    });

    expect(onChange).toHaveBeenCalledWith([{ kind: "maxAmount", basis: "dotacja" }]);
  });
});
