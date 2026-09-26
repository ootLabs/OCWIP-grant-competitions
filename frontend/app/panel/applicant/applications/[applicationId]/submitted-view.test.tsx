import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import type { Application, ApplicationForm, Attachment } from "@/lib/applicant-applications";

import { SubmittedView } from "./submitted-view";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

const application: Application = {
  id: "app-1",
  competitionId: "c1",
  formDefinitionId: "f1",
  status: "Submitted",
  answers: { tytul: "Warsztaty w świetlicy" },
  number: "001",
  submittedAt: "2026-09-20T10:00:00Z",
  lastSavedAt: "2026-09-20T10:00:00Z",
  checksum: "0a55-22c2-b414",
  isActive: true,
};

const form: ApplicationForm = {
  versionNumber: 2,
  document: {
    schemaVersion: 1,
    sections: [
      {
        key: "s1",
        title: "Projekt",
        description: "",
        fields: [
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
  },
};

const attachments: Attachment[] = [
  {
    id: "f1",
    applicationId: "app-1",
    fileName: "statut.pdf",
    contentType: "application/pdf",
    sizeInBytes: 1024,
    createdAt: "2026-09-19T10:00:00Z",
  },
];

afterEach(cleanup);

describe("SubmittedView", () => {
  it("shows the number, the competition, the confirmation download, and the frozen answers", () => {
    render(
      <SubmittedView
        application={application}
        form={form}
        competitionTitle="Granty na inicjatywy"
        attachments={attachments}
      />,
    );

    expect(screen.getByRole("heading", { level: 1, name: "Wniosek 001" })).toBeDefined();
    expect(screen.getByText("Granty na inicjatywy")).toBeDefined();
    expect(screen.getByText("0a55-22c2-b414")).toBeDefined();
    expect(screen.getByText("Warsztaty w świetlicy")).toBeDefined();

    const pdfLink = screen.getByRole("link", { name: /Pobierz potwierdzenie/ });
    expect(pdfLink.getAttribute("href")).toMatch(/\/applications\/app-1\/confirmation$/);
    const wholeLink = screen.getByRole("link", { name: "Pobierz cały wniosek (PDF)" });
    expect(wholeLink.getAttribute("href")).toMatch(/\/applications\/app-1\/pdf$/);

    const attachmentLink = screen.getByRole("link", { name: "statut.pdf" });
    expect(attachmentLink.getAttribute("href")).toMatch(/\/attachments\/f1$/);
  });

  it("says nothing was attached when there is nothing to list", () => {
    render(
      <SubmittedView
        application={application}
        form={form}
        competitionTitle="Granty na inicjatywy"
        attachments={[]}
      />,
    );

    expect(screen.getByText("Do wniosku nie dołączono plików.")).toBeDefined();
  });

  it("shows the result and the awarded amount once the results are approved", () => {
    render(
      <SubmittedView
        application={{ ...application, status: "Funded", awardedGrant: 6500 }}
        form={form}
        competitionTitle="Granty 2026"
        attachments={attachments}
      />,
    );

    expect(screen.getByRole("heading", { name: "Wynik konkursu" })).toBeDefined();
    expect(screen.getByText("Dofinansowany, umowa niepodpisana")).toBeDefined();
    expect(screen.getByRole("button", { name: "Przejdź do sprawozdania" })).toBeDefined();
    expect(screen.getByText(/Przyznana kwota: 6\s?500,00/)).toBeDefined();
  });

  it("says nothing about a result while the application is only submitted", () => {
    render(<SubmittedView application={application} form={form} competitionTitle="Granty 2026" attachments={attachments} />);

    expect(screen.queryByRole("heading", { name: "Wynik konkursu" })).toBeNull();
  });
});
