import { useState } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { AmountField } from "./amount-field";

afterEach(cleanup);

describe("AmountField", () => {
  it("spells out a normal amount", () => {
    render(<AmountField label="Kwota" value="100" onChange={() => {}} />);

    expect(screen.getByText("sto złotych")).toBeDefined();
  });

  it("does not crash when the typed amount is a billion or more", () => {
    // amountInWords only supports amounts below a billion
    // (lib/amount-in-words.ts); CompetitionRequestValidator allows far
    // larger ones, so this has to degrade to "no hint", not throw.
    render(
      <AmountField label="Kwota" value="1000000000" onChange={() => {}} />,
    );

    expect(screen.getByLabelText("Kwota")).toBeDefined();
  });

  it("updates the hint as the operator keeps typing", () => {
    function Wrapper() {
      const [value, setValue] = useState("");
      return <AmountField label="Kwota" value={value} onChange={setValue} />;
    }

    render(<Wrapper />);
    fireEvent.change(screen.getByLabelText("Kwota"), {
      target: { value: "5" },
    });

    expect(screen.getByText("pięć złotych")).toBeDefined();
  });
});
