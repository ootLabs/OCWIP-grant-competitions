import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import SubmittedApplicationPage from "./page";

vi.mock("next/navigation", () => ({
  useParams: () => ({ competitionId: "c1", applicationId: "a1" }),
}));

const offer = {
  id: "a1",
  competitionId: "c1",
  competitionTitle: "Granty na inicjatywy",
  number: "001",
  entityName: "Stowarzyszenie Pod Dębem",
  entityType: "InformalGroup",
  status: "Submitted",
  submittedAt: "2026-09-15T10:30:00Z",
  checksum: "0a55-22c2-b414",
  formVersion: 2,
  definition: {
    schemaVersion: 1,
    sections: [
      {
        key: "projekt",
        title: "Informacje o projekcie",
        description: "",
        fields: [
          { key: "tytul", type: "shortText", label: "Tytuł projektu", help: "", required: true, printed: true, maxLength: 200 },
          { key: "ma_patrona", type: "yesNo", label: "Czy grupa ma patrona", help: "", required: true, printed: true },
          {
            key: "patron",
            type: "shortText",
            label: "Nazwa patrona",
            help: "",
            required: false,
            printed: true,
            maxLength: 200,
            visibleWhen: { field: "ma_patrona", equalsAnyOf: ["true"] },
          },
          { key: "opis", type: "longText", label: "Opis", help: "", required: false, printed: true, maxLength: 500 },
          {
            key: "budzet",
            type: "repeatableTable",
            label: "Budżet",
            help: "",
            required: true,
            printed: true,
            table: {
              columns: [
                { key: "nazwa", type: "shortText", label: "Pozycja", help: "", required: true, printed: true, maxLength: 100 },
                { key: "liczba", type: "number", label: "Liczba", help: "", required: true, printed: true },
                { key: "cena", type: "amount", label: "Cena", help: "", required: true, printed: true },
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
        ],
      },
    ],
  },
  answers: {
    tytul: "Warsztaty w świetlicy",
    ma_patrona: false,
    patron: "Zostało sprzed ukrycia",
    budzet: [{ nazwa: "Materiały", liczba: 3, cena: 150 }],
  },
  attachments: [
    { id: "f1", applicationId: "a1", fileName: "statut.pdf", contentType: "application/pdf", sizeInBytes: 1048576, createdAt: "2026-09-14T10:00:00Z" },
  ],
};

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async () => new Response(JSON.stringify(body), { status })),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("SubmittedApplicationPage", () => {
  it("shows the offer as submitted, with its technical block and its attachments", async () => {
    respondWith(offer);

    render(<SubmittedApplicationPage />);

    expect(await screen.findByRole("heading", { level: 1, name: "Wniosek 001: Stowarzyszenie Pod Dębem" })).toBeDefined();
    expect(screen.getByText("Grupa nieformalna")).toBeDefined();
    expect(screen.getByText("0a55-22c2-b414")).toBeDefined();
    expect(screen.getByText("2")).toBeDefined();

    const file = screen.getByRole("link", { name: "statut.pdf" });
    expect(file.getAttribute("href")).toMatch(/\/attachments\/f1$/);

    expect(screen.getByText("Warsztaty w świetlicy")).toBeDefined();
    expect(screen.getByText("Nie")).toBeDefined();
    expect(screen.getByText("brak odpowiedzi")).toBeDefined();
    expect(screen.getByText("Materiały")).toBeDefined();
    expect(screen.getByText(/450,00/)).toBeDefined();
  });

  it("leaves out what a condition hides, even if an answer is left in it", async () => {
    respondWith(offer);

    render(<SubmittedApplicationPage />);

    await screen.findByText("Warsztaty w świetlicy");
    expect(screen.queryByText("Nazwa patrona")).toBeNull();
    expect(screen.queryByText("Zostało sprzed ukrycia")).toBeNull();
  });

  it("does not open a draft or an offer of another competition", async () => {
    respondWith({ title: "W tym konkursie nie ma takiego złożonego wniosku." }, 404);

    render(<SubmittedApplicationPage />);

    expect(
      await screen.findByText("W tym konkursie nie ma takiego złożonego wniosku"),
    ).toBeDefined();
    expect(screen.getByRole("link", { name: "Wróć do listy wniosków" }).getAttribute("href")).toBe(
      "/panel/operator/applications/c1",
    );
  });
});
