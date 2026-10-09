import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import FormBuilderPage from "./page";
import { loadDraft, saveDraft } from "@/lib/forms/draft-storage";
import type { FormDocument } from "@/lib/forms/document-types";

let competitionId = "target-1";

vi.mock("next/navigation", () => ({
  useParams: () => ({ competitionId }),
}));

const testDocument: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "wnioskodawca",
      title: "Dane wnioskodawcy",
      description: "",
      fields: [
        {
          key: "forma_prawna",
          type: "singleChoice",
          label: "Forma prawna",
          help: "",
          required: true,
          printed: true,
          options: [{ value: "inna", label: "Inna" }],
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
          help: "",
          required: true,
          printed: true,
          maxLength: 200,
        },
      ],
    },
  ],
};

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status });
}

function competition(overrides: Record<string, unknown>) {
  return {
    id: "id",
    number: "0/2026",
    title: "Konkurs",
    formDefinitionId: null,
    ...overrides,
  };
}

/** Routes a fetch mock call to a canned answer by matching the URL and method. */
function stubApi(overrides: { onPublish?: () => Response } = {}) {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? "GET";

      if (method === "POST" && url.endsWith("/competitions/target-1/form-definitions")) {
        return overrides.onPublish
          ? overrides.onPublish()
          : jsonResponse(
              {
                id: "fd2",
                competitionId: "target-1",
                versionNumber: 1,
                definition: testDocument,
                isCurrent: true,
                createdAt: "2026-01-03T00:00:00Z",
              },
              201,
            );
      }
      if (url.endsWith("/competitions/target-1/form-definitions")) {
        return jsonResponse([]);
      }
      if (url.endsWith("/competitions/target-1")) {
        return jsonResponse(
          competition({ id: "target-1", number: "2/2026", formDefinitionId: null, maxGrantAmount: 9000 }),
        );
      }
      if (url.endsWith("/competitions")) {
        return jsonResponse([
          competition({ id: "target-1", number: "2/2026", formDefinitionId: null }),
          competition({ id: "source-1", number: "1/2026", title: "Konkurs źródłowy", formDefinitionId: "fd1" }),
        ]);
      }
      if (url.endsWith("/competitions/source-1/form-definitions")) {
        return jsonResponse([
          { id: "fd1", competitionId: "source-1", versionNumber: 1, isCurrent: true, createdAt: "2026-01-01T00:00:00Z" },
        ]);
      }
      if (url.endsWith("/competitions/source-1/form-definitions/1")) {
        return jsonResponse({
          id: "fd1",
          competitionId: "source-1",
          versionNumber: 1,
          definition: testDocument,
          isCurrent: true,
          createdAt: "2026-01-01T00:00:00Z",
        });
      }

      throw new Error(`Unexpected fetch: ${method} ${url}`);
    }),
  );
}

async function copyFromSource() {
  render(<FormBuilderPage />);

  const select = await screen.findByLabelText("Skopiuj formularz z konkursu");
  fireEvent.change(select, { target: { value: "source-1" } });
  fireEvent.click(screen.getByRole("button", { name: "Kopiuj formularz" }));

  await screen.findByText("Tytuł projektu");
}

beforeEach(() => {
  competitionId = "target-1";
  window.localStorage.clear();
  stubApi();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FormBuilderPage", () => {
  it("offers a source competition to copy from when the competition has no form", async () => {
    render(<FormBuilderPage />);

    expect(await screen.findByText(/Konkurs źródłowy/)).toBeDefined();
  });

  it("opens the copied document in the builder", async () => {
    await copyFromSource();

    expect(screen.getByDisplayValue("Dane wnioskodawcy")).toBeDefined();
    expect(screen.getByText("Jaka forma prawna")).toBeDefined();
  });

  it("lets an operator edit a field's label", async () => {
    await copyFromSource();

    fireEvent.click(screen.getByText("Tytuł projektu"));
    const label = screen.getByDisplayValue("Tytuł projektu");
    fireEvent.change(label, { target: { value: "Nowy tytuł projektu" } });

    expect(await screen.findByText("Nowy tytuł projektu")).toBeDefined();
  });

  it("adds a field to a section without any technical input", async () => {
    await copyFromSource();

    fireEvent.click(screen.getByRole("button", { name: "Dodaj pole" }));
    fireEvent.change(screen.getByLabelText("Etykieta pola"), {
      target: { value: "Krótki opis" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Dodaj" }));

    expect(await screen.findByText("Krótki opis")).toBeDefined();
  });

  it("blocks deleting a field another field's visibility depends on", async () => {
    await copyFromSource();

    const row = screen.getByText("Forma prawna").closest("li")!;
    const removeButton = within(row).getByRole("button", { name: "Usuń" });

    expect(removeButton.hasAttribute("disabled")).toBe(true);
    fireEvent.click(removeButton);
    expect(screen.getByText("Forma prawna")).toBeDefined();
  });

  it("moves a field down among its siblings", async () => {
    await copyFromSource();

    const list = screen.getByText("Forma prawna").closest("ul")!;
    const labelsBefore = within(list)
      .getAllByRole("button", { expanded: false })
      .map((button) => button.textContent);
    expect(labelsBefore[0]).toBe("Forma prawna");

    fireEvent.click(within(list).getAllByRole("button", { name: /Przesuń w dół/ })[0]);

    const labelsAfter = within(list)
      .getAllByRole("button", { expanded: false })
      .map((button) => button.textContent);
    expect(labelsAfter[0]).toBe("Jaka forma prawna");
    expect(labelsAfter[1]).toBe("Forma prawna");
  });

  it("keeps the draft after the page remounts, without asking the network again", async () => {
    await copyFromSource();

    fireEvent.click(screen.getByText("Tytuł projektu"));
    fireEvent.change(screen.getByDisplayValue("Tytuł projektu"), {
      target: { value: "Zmieniony tytuł" },
    });
    await screen.findByText("Zmieniony tytuł");

    cleanup();

    const draft = loadDraft("target-1");
    expect(draft?.document.sections[0].fields.some((f) => f.label === "Zmieniony tytuł")).toBe(
      true,
    );

    render(<FormBuilderPage />);
    expect(await screen.findByText("Zmieniony tytuł")).toBeDefined();
  });

  it("switches to a preview that renders the same fields through FormRenderer", async () => {
    await copyFromSource();

    fireEvent.click(screen.getByRole("tab", { name: "Podgląd" }));

    expect(await screen.findByText(/To jest podgląd/)).toBeDefined();
    expect(screen.getByLabelText(/^Tytuł projektu/)).toBeDefined();
  });

  it("publishes after confirmation, clears the draft and reports the new version", async () => {
    await copyFromSource();

    fireEvent.click(screen.getByRole("button", { name: "Opublikuj formularz" }));
    fireEvent.click(screen.getByRole("button", { name: "Tak, opublikuj" }));

    expect(await screen.findByText(/Opublikowano wersję 1/)).toBeDefined();
    expect(loadDraft("target-1")).toBeNull();
  });

  it("does not publish without the confirmation step", async () => {
    await copyFromSource();

    fireEvent.click(screen.getByRole("button", { name: "Opublikuj formularz" }));
    fireEvent.click(screen.getByRole("button", { name: "Anuluj" }));

    expect(screen.queryByText(/Opublikowano wersję/)).toBeNull();
    expect(screen.getByRole("button", { name: "Opublikuj formularz" })).toBeDefined();
  });

  it("shows the contract gate's field errors when publishing a rejected document", async () => {
    stubApi({
      onPublish: () =>
        new Response(
          JSON.stringify({ errors: { "$.sections[0].fields[0]": ["Pole nie ma etykiety."] } }),
          { status: 400, headers: { "content-type": "application/problem+json" } },
        ),
    });
    await copyFromSource();

    fireEvent.click(screen.getByRole("button", { name: "Opublikuj formularz" }));
    fireEvent.click(screen.getByRole("button", { name: "Tak, opublikuj" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toMatch(/Pole nie ma etykiety/);
  });

  it("shows the backend's own reason for a 409, not a guessed one", async () => {
    stubApi({
      onPublish: () =>
        new Response(
          JSON.stringify({ detail: "Ten konkurs jest oznaczony jako nieaktywny." }),
          { status: 409, headers: { "content-type": "application/problem+json" } },
        ),
    });
    await copyFromSource();

    fireEvent.click(screen.getByRole("button", { name: "Opublikuj formularz" }));
    fireEvent.click(screen.getByRole("button", { name: "Tak, opublikuj" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toMatch(/oznaczony jako nieaktywny/);
  });

  it("lets an operator publish a second version after editing past the first", async () => {
    await copyFromSource();

    fireEvent.click(screen.getByRole("button", { name: "Opublikuj formularz" }));
    fireEvent.click(screen.getByRole("button", { name: "Tak, opublikuj" }));
    await screen.findByText(/Opublikowano wersję 1/);

    fireEvent.click(screen.getByText(/^Tytuł projektu/));
    fireEvent.change(screen.getByDisplayValue("Tytuł projektu"), {
      target: { value: "Tytuł po publikacji" },
    });

    // The confirmation from version 1 has to make way for the button again,
    // or there would be no way to publish the edit as version 2.
    expect(screen.queryByText(/Opublikowano wersję/)).toBeNull();
    expect(await screen.findByRole("button", { name: "Opublikuj formularz" })).toBeDefined();
  });
});

/**
 * A form whose second section is shown only when the first one was answered a
 * certain way. Reordering or deleting either half breaks the other, which is
 * what the guards in lib/forms/section-guards.ts are for (T-26a).
 */
const dependentSections: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "zgody",
      title: "Zgody",
      description: "",
      fields: [
        { key: "ma_patrona", type: "yesNo", label: "Ma patrona", help: "", required: true, printed: true },
      ],
    },
    {
      key: "patron",
      title: "Dane patrona",
      description: "",
      visibleWhen: { field: "ma_patrona", equalsAnyOf: ["true"] },
      fields: [
        {
          key: "nazwa_patrona",
          type: "shortText",
          label: "Nazwa patrona",
          help: "",
          required: true,
          printed: true,
          maxLength: 200,
        },
      ],
    },
  ],
};

async function startBlank() {
  render(<FormBuilderPage />);
  fireEvent.click(await screen.findByRole("button", { name: "Zacznij od zera" }));
  await screen.findByDisplayValue("Sekcja 1");
}

describe("FormBuilderPage, sections and fixed rows (T-26a)", () => {
  it("starts a form from nothing, without copying a competition", async () => {
    await startBlank();

    expect(screen.getByText("Sekcja 1 z 1")).toBeDefined();
    // One section, not none: a form without a single section is refused on
    // publication, so the operator never starts from something invalid.
    expect(loadDraft("target-1")?.document.sections).toHaveLength(1);
  });

  it("adds a section and moves it up", async () => {
    await startBlank();

    fireEvent.click(screen.getByRole("button", { name: "Dodaj sekcję" }));
    fireEvent.change(screen.getByLabelText("Tytuł nowej sekcji"), {
      target: { value: "Budżet projektu" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Dodaj" }));

    await screen.findByDisplayValue("Budżet projektu");
    expect(screen.getByText("Sekcja 2 z 2")).toBeDefined();

    fireEvent.click(screen.getByRole("button", { name: "Przesuń sekcję w górę: Budżet projektu" }));

    await waitFor(() => {
      const titles = screen
        .getAllByLabelText("Tytuł sekcji")
        .map((input) => (input as HTMLInputElement).value);
      expect(titles).toEqual(["Budżet projektu", "Sekcja 1"]);
    });
  });

  it("will not remove the only section a form has", async () => {
    await startBlank();

    const remove = screen.getByRole("button", { name: "Usuń sekcję: Sekcja 1" });
    expect(remove.hasAttribute("disabled")).toBe(true);
    expect(screen.getByText(/To jedyna sekcja formularza/)).toBeDefined();
  });

  it("removes a section once the form has more than one", async () => {
    await startBlank();

    fireEvent.click(screen.getByRole("button", { name: "Dodaj sekcję" }));
    fireEvent.change(screen.getByLabelText("Tytuł nowej sekcji"), {
      target: { value: "Do usunięcia" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Dodaj" }));
    await screen.findByDisplayValue("Do usunięcia");

    fireEvent.click(screen.getByRole("button", { name: "Usuń sekcję: Do usunięcia" }));

    await waitFor(() => expect(screen.queryByDisplayValue("Do usunięcia")).toBeNull());
    expect(screen.getByText("Sekcja 1 z 1")).toBeDefined();
    // O-01: the focus lands on the section left in its place, not on <body>.
    await waitFor(() =>
      expect(document.activeElement?.getAttribute("aria-label")).toMatch(/^Sekcja 1: /),
    );
  });

  it("asks before throwing the whole draft away (O-02)", async () => {
    HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
      this.setAttribute("open", "");
    };
    await startBlank();
    fireEvent.click(screen.getByRole("button", { name: "Dodaj sekcję" }));
    fireEvent.change(screen.getByLabelText("Tytuł nowej sekcji"), { target: { value: "Zostaje" } });
    fireEvent.click(screen.getByRole("button", { name: "Dodaj" }));
    await screen.findByDisplayValue("Zostaje");

    fireEvent.click(screen.getByRole("button", { name: "Odrzuć szkic i zacznij od nowa" }));
    fireEvent.click(screen.getByRole("button", { name: "Wróć" }));

    expect(screen.getByDisplayValue("Zostaje")).toBeDefined();
  });

  it("blocks the move that would put a condition above the answer it reads", async () => {
    saveDraft("target-1", dependentSections, null);
    render(<FormBuilderPage />);

    await screen.findByDisplayValue("Dane patrona");

    const up = screen.getByRole("button", { name: "Przesuń sekcję w górę: Dane patrona" });
    expect(up.hasAttribute("disabled")).toBe(true);

    // The same break seen from the other end: pushing the answer down. Both
    // sections carry the explanation, because either button is the one the
    // operator may have just tried to press.
    const down = screen.getByRole("button", { name: "Przesuń sekcję w dół: Zgody" });
    expect(down.hasAttribute("disabled")).toBe(true);

    expect(screen.getAllByText(/czytałoby odpowiedź "Ma patrona" spod siebie/)).toHaveLength(2);
  });

  it("blocks removing a section whose answer another section reads", async () => {
    saveDraft("target-1", dependentSections, null);
    render(<FormBuilderPage />);

    await screen.findByDisplayValue("Zgody");

    const remove = screen.getByRole("button", { name: "Usuń sekcję: Zgody" });
    expect(remove.hasAttribute("disabled")).toBe(true);
    expect(screen.getByText(/Odpowiedzi z tej sekcji czytają: sekcja "Dane patrona"/)).toBeDefined();
  });

  it("gives a table of fixed size its rows, which it had no way to get before", async () => {
    await startBlank();

    fireEvent.click(screen.getByRole("button", { name: "Dodaj pole" }));
    fireEvent.change(screen.getByLabelText("Rodzaj pola"), { target: { value: "fixedTable" } });
    fireEvent.change(screen.getByLabelText("Etykieta pola"), {
      target: { value: "Członkowie grupy" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Dodaj" }));

    fireEvent.click(await screen.findByText("Członkowie grupy"));

    // A fixed table without a single row is refused on publication, so the
    // editor says so rather than letting the operator find out later.
    expect(await screen.findByText(/musi mieć co najmniej jeden wiersz/)).toBeDefined();

    fireEvent.change(screen.getByLabelText("Nazwa nowego wiersza"), {
      target: { value: "Pierwszy członek" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^Dodaj wiersz/ }));

    await waitFor(() =>
      expect(loadDraft("target-1")?.document.sections[0].fields[0].table?.rows).toEqual([
        { key: "pierwszy_czlonek", label: "Pierwszy członek" },
      ]),
    );
  });
});
