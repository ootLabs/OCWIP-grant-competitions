import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { FormRenderer } from "./form-renderer";
import type { FormDocument } from "@/lib/forms/document-types";

const document: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "wnioskodawca",
      title: "Wnioskodawca",
      description: "",
      fields: [
        {
          key: "forma_prawna",
          type: "singleChoice",
          label: "Forma prawna",
          help: "",
          required: true,
          printed: true,
          options: [
            { value: "stowarzyszenie", label: "Stowarzyszenie" },
            { value: "inna", label: "Inna" },
          ],
        },
        {
          key: "forma_prawna_inna",
          type: "shortText",
          label: "Jaka forma prawna",
          help: "",
          required: true,
          printed: true,
          maxLength: 200,
          visibleWhen: { field: "forma_prawna", equalsAnyOf: ["inna"] },
        },
        {
          key: "tytul",
          type: "shortText",
          label: "Tytuł projektu",
          help: "Krótko i konkretnie",
          required: true,
          printed: true,
          maxLength: 10,
        },
      ],
    },
    {
      key: "budzet",
      title: "Budżet",
      description: "",
      fields: [
        {
          key: "budzet_a",
          type: "repeatableTable",
          label: "Koszty bezpośrednie",
          help: "",
          required: true,
          printed: true,
          table: {
            minRows: 1,
            columns: [
              { key: "liczba", type: "number", label: "Liczba jednostek", help: "", required: true, printed: true },
              { key: "cena", type: "amount", label: "Cena jednostkowa", help: "", required: true, printed: true },
              {
                key: "wartosc",
                type: "calculated",
                label: "Wartość",
                help: "",
                required: false,
                printed: true,
                calculation: { kind: "product", operands: ["liczba", "cena"] },
              },
            ],
          },
        },
        {
          key: "suma_a",
          type: "calculated",
          label: "Suma A",
          help: "",
          required: false,
          printed: true,
          calculation: { kind: "sum", operands: ["budzet_a.wartosc"] },
          limits: [{ kind: "maxAmount", basis: "competition.maxGrantAmount" }],
        },
      ],
    },
    {
      key: "grupa",
      title: "Grupa",
      description: "",
      visibleWhen: { field: "forma_prawna", equalsAnyOf: ["grupa_nieformalna"] },
      fields: [],
    },
  ],
};

afterEach(cleanup);

describe("FormRenderer", () => {
  it("renders the current section's title and hides the rest", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);

    expect(screen.getByRole("heading", { name: "Wnioskodawca" })).toBeDefined();
    expect(screen.queryByText("Koszty bezpośrednie")).toBeNull();
  });

  it("says a field is required in words, not only with an asterisk hidden from screen readers", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);

    expect(screen.getByRole("textbox", { name: "Tytuł projektu (wymagane)" })).toBeDefined();
    expect(screen.getByRole("group", { name: "Forma prawna (wymagane)" })).toBeDefined();
    expect(screen.getByText("Pola oznaczone gwiazdką (*) są wymagane.")).toBeDefined();
  });

  it("says a table column is required in each of its cells, which are named apart from the header", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);
    fireEvent.click(screen.getByRole("button", { name: /Budżet/ }));
    fireEvent.click(screen.getByRole("button", { name: "Dodaj wiersz" }));

    expect(screen.getByRole("columnheader", { name: "Liczba jednostek (wymagane)" })).toBeDefined();
    expect(screen.getByLabelText("Liczba jednostek (wymagane), wiersz 1")).toBeDefined();
  });

  it("leaves a section out of the nav entirely when its own condition is not met", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);
    expect(screen.queryByText(/Grupa/)).toBeNull();
  });

  it("shows a conditional field once its condition is met, without losing an earlier answer", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);

    fireEvent.change(screen.getByLabelText(/^Tytuł projektu/), { target: { value: "Mój tytuł" } });
    expect(screen.queryByLabelText(/^Jaka forma prawna/)).toBeNull();

    fireEvent.click(screen.getByLabelText("Inna"));
    expect(screen.getByLabelText(/^Jaka forma prawna/)).toBeDefined();

    // The earlier answer, in a field that never re-mounted, is untouched.
    expect((screen.getByLabelText(/^Tytuł projektu/) as HTMLInputElement).value).toBe("Mój tytuł");
  });

  it("hides a field when its condition stops being met, and keeps its value for when it returns", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);

    fireEvent.click(screen.getByLabelText("Inna"));
    fireEvent.change(screen.getByLabelText(/^Jaka forma prawna/), { target: { value: "Spółdzielnia" } });
    fireEvent.click(screen.getByLabelText("Stowarzyszenie"));

    expect(screen.queryByLabelText(/^Jaka forma prawna/)).toBeNull();

    fireEvent.click(screen.getByLabelText("Inna"));
    // The card's own criterion: a conditional field hides and shows "bez
    // gubienia wpisanej wartości" (without losing what was typed).
    expect((screen.getByLabelText(/^Jaka forma prawna/) as HTMLInputElement).value).toBe(
      "Spółdzielnia",
    );
  });

  it("shows a character counter for a field with a maxLength", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);

    fireEvent.change(screen.getByLabelText(/^Tytuł projektu/), { target: { value: "12345" } });
    expect(screen.getByText("5 z 10")).toBeDefined();
  });

  it("shows an error only once the field has been left, and clears it once fixed", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);

    const input = screen.getByLabelText(/^Tytuł projektu/);
    expect(screen.queryByRole("alert")).toBeNull();

    fireEvent.blur(input);
    expect(screen.getByRole("alert").textContent).toMatch(/wymagane/);

    fireEvent.change(input, { target: { value: "OK" } });
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("shows every section's status in the nav, gotowa once its required fields are filled", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);

    fireEvent.change(screen.getByLabelText(/^Tytuł projektu/), { target: { value: "1234567890" } });
    fireEvent.click(screen.getByLabelText("Stowarzyszenie"));

    const firstTab = screen.getByRole("button", { name: /Wnioskodawca/ });
    expect(firstTab.textContent).toMatch(/gotowa/);
  });

  it("computes the budget row's value live and shows it read only", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);
    fireEvent.click(screen.getByRole("button", { name: /Budżet/ }));

    fireEvent.click(screen.getByRole("button", { name: "Dodaj wiersz" }));
    fireEvent.change(screen.getByLabelText("Liczba jednostek (wymagane), wiersz 1"), { target: { value: "3" } });
    fireEvent.change(screen.getByLabelText("Cena jednostkowa (wymagane), wiersz 1"), { target: { value: "100" } });

    expect(screen.getByText(/300,00.zł/)).toBeDefined();
  });

  it("computes a top level calculated field's own value live, not just its error", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);
    fireEvent.click(screen.getByRole("button", { name: /Budżet/ }));

    fireEvent.click(screen.getByRole("button", { name: "Dodaj wiersz" }));
    fireEvent.change(screen.getByLabelText("Liczba jednostek (wymagane), wiersz 1"), { target: { value: "3" } });
    fireEvent.change(screen.getByLabelText("Cena jednostkowa (wymagane), wiersz 1"), { target: { value: "100" } });

    expect((screen.getByLabelText(/^Suma A/) as HTMLInputElement).value).toMatch(/300,00.zł/);
  });

  it("adds, reorders and removes rows of a repeatable table", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);
    fireEvent.click(screen.getByRole("button", { name: /Budżet/ }));

    fireEvent.click(screen.getByRole("button", { name: "Dodaj wiersz" }));
    fireEvent.change(screen.getByLabelText("Liczba jednostek (wymagane), wiersz 1"), { target: { value: "1" } });
    fireEvent.click(screen.getByRole("button", { name: "Dodaj wiersz" }));
    fireEvent.change(screen.getByLabelText("Liczba jednostek (wymagane), wiersz 2"), { target: { value: "2" } });

    fireEvent.click(screen.getByLabelText("Przesuń wiersz 1 w dół"));
    expect((screen.getByLabelText("Liczba jednostek (wymagane), wiersz 1") as HTMLInputElement).value).toBe("2");

    fireEvent.click(screen.getAllByRole("button", { name: "Usuń wiersz" })[0]);
    expect(screen.queryByLabelText("Liczba jednostek (wymagane), wiersz 2")).toBeNull();
  });

  it("keeps a table cell's error attached to its own data when rows are reordered", () => {
    render(<FormRenderer document={document} competitionSettings={{}} />);
    fireEvent.click(screen.getByRole("button", { name: /Budżet/ }));

    fireEvent.click(screen.getByRole("button", { name: "Dodaj wiersz" }));
    fireEvent.change(screen.getByLabelText("Liczba jednostek (wymagane), wiersz 1"), { target: { value: "1" } });
    // Touched and left empty: this cell now has a visible error.
    fireEvent.blur(screen.getByLabelText("Cena jednostkowa (wymagane), wiersz 1"));

    fireEvent.click(screen.getByRole("button", { name: "Dodaj wiersz" }));
    fireEvent.change(screen.getByLabelText("Liczba jednostek (wymagane), wiersz 2"), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText("Cena jednostkowa (wymagane), wiersz 2"), { target: { value: "50" } });

    const rowOf = (label: string) => screen.getByLabelText(label).closest("tr")!;
    expect(within(rowOf("Liczba jednostek (wymagane), wiersz 1")).getByRole("alert")).toBeDefined();
    expect(within(rowOf("Liczba jednostek (wymagane), wiersz 2")).queryByRole("alert")).toBeNull();

    fireEvent.click(screen.getByLabelText("Przesuń wiersz 1 w dół"));

    // The data swapped: row 1 now holds what was row 2's filled data.
    expect((screen.getByLabelText("Cena jednostkowa (wymagane), wiersz 1") as HTMLInputElement).value).toBe(
      "50",
    );
    // The error follows the data, not the row position it used to sit in.
    expect(within(rowOf("Liczba jednostek (wymagane), wiersz 1")).queryByRole("alert")).toBeNull();
    expect(within(rowOf("Liczba jednostek (wymagane), wiersz 2")).getByRole("alert")).toBeDefined();
  });

  it("flags an over-limit sum with the concrete ceiling, per D12", () => {
    render(<FormRenderer document={document} competitionSettings={{ maxGrantAmount: 100 }} />);
    fireEvent.click(screen.getByRole("button", { name: /Budżet/ }));

    fireEvent.click(screen.getByRole("button", { name: "Dodaj wiersz" }));
    fireEvent.change(screen.getByLabelText("Liczba jednostek (wymagane), wiersz 1"), { target: { value: "10" } });
    fireEvent.change(screen.getByLabelText("Cena jednostkowa (wymagane), wiersz 1"), { target: { value: "100" } });
    fireEvent.blur(screen.getByLabelText("Cena jednostkowa (wymagane), wiersz 1"));

    const alert = screen.getAllByRole("alert").find((el) => /Przekroczono/.test(el.textContent ?? ""));
    expect(alert).toBeDefined();
    expect(alert?.textContent).toMatch(/100,00.zł/);

    const budgetTab = screen.getByRole("button", { name: /Budżet/ });
    expect(budgetTab.textContent).toMatch(/są błędy/);
  });
});

describe("every field kind", () => {
  const oneOfEach: FormDocument = {
    schemaVersion: 1,
    sections: [
      {
        key: "s1",
        title: "Wszystkie rodzaje",
        description: "",
        fields: [
          { key: "a", type: "shortText", label: "Tekst krótki", help: "", required: false, printed: true, maxLength: 50 },
          { key: "b", type: "longText", label: "Tekst długi", help: "", required: false, printed: true, maxLength: 500 },
          { key: "c", type: "number", label: "Liczba", help: "", required: false, printed: true },
          { key: "d", type: "amount", label: "Kwota", help: "", required: false, printed: true },
          { key: "e", type: "percent", label: "Procent", help: "", required: false, printed: true },
          { key: "f", type: "date", label: "Data", help: "", required: false, printed: true },
          { key: "g", type: "dateTime", label: "Data i godzina", help: "", required: false, printed: true },
          { key: "h", type: "yesNo", label: "Tak albo nie", help: "", required: false, printed: true },
          {
            key: "i",
            type: "singleChoice",
            label: "Wybór jednej opcji",
            help: "",
            required: false,
            printed: true,
            options: [{ value: "x", label: "Jedna opcja" }],
          },
          {
            key: "j",
            type: "multipleChoice",
            label: "Wybór wielu opcji",
            help: "",
            required: false,
            printed: true,
            options: [{ value: "x", label: "Wiele opcji" }],
          },
          {
            key: "k",
            type: "repeatableTable",
            label: "Tabela zmienna",
            help: "",
            required: false,
            printed: true,
            table: { columns: [{ key: "col", type: "shortText", label: "Kolumna", help: "", required: false, printed: true, maxLength: 10 }] },
          },
          {
            key: "l",
            type: "fixedTable",
            label: "Tabela stała",
            help: "",
            required: false,
            printed: true,
            table: {
              rows: [{ key: "r1", label: "Wiersz 1" }],
              columns: [{ key: "col", type: "shortText", label: "Kolumna", help: "", required: false, printed: true, maxLength: 10 }],
            },
          },
          {
            key: "m",
            type: "file",
            label: "Plik",
            help: "",
            required: false,
            printed: true,
            file: { allowedFormats: ["pdf"], maxSizeMegabytes: 10 },
          },
          { key: "n", type: "statement", label: "Oświadczenie", help: "", required: false, printed: false, statementText: "Oświadczam, że tak." },
          { key: "o", type: "calculated", label: "Wyliczane", help: "", required: false, printed: true, calculation: { kind: "sum", operands: [] } },
        ],
      },
    ],
  };

  afterEach(cleanup);

  it("renders every one of the fifteen kinds with a fillable, labelled control", () => {
    render(<FormRenderer document={oneOfEach} competitionSettings={{}} />);

    expect(screen.getByLabelText(/^Tekst krótki/)).toBeDefined();
    expect(screen.getByLabelText(/^Tekst długi/)).toBeDefined();
    expect(screen.getByLabelText(/^Liczba/)).toBeDefined();
    expect(screen.getByLabelText(/^Kwota/)).toBeDefined();
    expect(screen.getByLabelText(/^Procent/)).toBeDefined();
    expect(screen.getByLabelText("Data")).toBeDefined();
    expect(screen.getByLabelText(/^Data i godzina/)).toBeDefined();
    expect(screen.getByText("Tak")).toBeDefined();
    expect(screen.getByText("Jedna opcja")).toBeDefined();
    expect(screen.getByText("Wiele opcji")).toBeDefined();
    expect(screen.getByRole("button", { name: "Dodaj wiersz" })).toBeDefined();
    expect(screen.getByText("Wiersz 1")).toBeDefined();
    expect(screen.getByLabelText(/^Plik/)).toBeDefined();
    expect(screen.getByText("Oświadczam, że tak.")).toBeDefined();
    expect(screen.getByLabelText(/^Wyliczane/)).toBeDefined();

    fireEvent.change(screen.getByLabelText(/^Tekst krótki/), { target: { value: "abc" } });
    expect((screen.getByLabelText(/^Tekst krótki/) as HTMLInputElement).value).toBe("abc");
  });
});

describe("fixed table", () => {
  const fixedDocument: FormDocument = {
    schemaVersion: 1,
    sections: [
      {
        key: "grupa",
        title: "Grupa",
        description: "",
        fields: [
          {
            key: "czlonkowie",
            type: "fixedTable",
            label: "Członkowie grupy",
            help: "",
            required: true,
            printed: true,
            table: {
              rows: [
                { key: "lider", label: "Lider" },
                { key: "czlonek_2", label: "Członek 2" },
                { key: "czlonek_3", label: "Członek 3" },
              ],
              columns: [
                { key: "imie", type: "shortText", label: "Imię i nazwisko", help: "", required: true, printed: true, maxLength: 100 },
              ],
            },
          },
        ],
      },
    ],
  };

  afterEach(cleanup);

  it("shows exactly its own rows, named, with no way to add or remove one", () => {
    render(<FormRenderer document={fixedDocument} competitionSettings={{}} />);

    expect(screen.getByText("Lider")).toBeDefined();
    expect(screen.getByText("Członek 3")).toBeDefined();
    expect(screen.queryByRole("button", { name: /Dodaj wiersz/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /Usuń wiersz/ })).toBeNull();
  });

  it("lets every named row be filled", () => {
    render(<FormRenderer document={fixedDocument} competitionSettings={{}} />);
    const row = screen.getByText("Lider").closest("tr")!;
    fireEvent.change(within(row).getByLabelText(/Imię i nazwisko/), {
      target: { value: "Jan Kowalski" },
    });
    expect((within(row).getByLabelText(/Imię i nazwisko/) as HTMLInputElement).value).toBe(
      "Jan Kowalski",
    );
  });
});
