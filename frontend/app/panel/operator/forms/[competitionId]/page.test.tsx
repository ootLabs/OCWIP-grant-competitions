import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import FormBuilderPage from "./page";
import { loadDraft } from "@/lib/forms/draft-storage";
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
