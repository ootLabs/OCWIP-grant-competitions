import { expect, type Browser } from "@playwright/test";

import { registerAndVerify, signIn } from "../lib/accounts";
import { json, onScreen, pdf, person, type Submitted } from "../lib/api";
import { apiUrl, run } from "../lib/env";
import { waitForMail } from "../lib/mailpit";
import { answers2026, type ApplicantKind } from "../fixtures/answers-2026";

/** Registers, fills the card and the form, uploads the statute and submits through the screen. */
export async function submit(browser: Browser, kind: ApplicantKind, competitionId: string, requirementId: string): Promise<Submitted> {
  const applicant = person(kind);
  const context = await browser.newContext();
  const page = await context.newPage();
  await registerAndVerify(page, applicant);
  await signIn(page, applicant.email);
  const api = context.request;

  // The public page offers the intake to somebody signed in.
  await page.goto(`/competitions/${competitionId}`);
  await expect(page.getByRole("heading", { level: 1 })).toContainText(`Konkurs E2E ${run}`);

  const entityName = kind === "Organisation" ? `Stowarzyszenie E2E ${run}` : `Sąsiedzi z Zaodrza ${run}`;
  const card =
    kind === "Organisation"
      ? {
          type: kind,
          name: entityName,
          legalForm: "Association",
          register: "Krs",
          registerNumber: "0000000001",
          nip: "111-111-11-11",
          address: "ul. Testowa 1, 45-000 Opole",
          phone: "+48 700 100 200",
          email: "biuro@example.org",
          bankAccount: "PL73 1111 1111 1111 1111 1111 1111",
          representatives: [{ firstName: "Anna", lastName: "Testowa", function: "Prezeska" }],
        }
      : { type: kind, name: entityName };
  await json(await api.post(`${apiUrl}/me/entity`, { data: card }));

  const draft = await json<{ id: string }>(await api.post(`${apiUrl}/competitions/${competitionId}/applications`));
  await json(await api.put(`${apiUrl}/applications/${draft.id}`, {
    data: { answers: answers2026(kind, `Ogród sąsiedzki ${kind} ${run}`) },
  }));
  await json(await api.post(`${apiUrl}/applications/${draft.id}/attachments`, {
    multipart: {
      file: { name: "statut.pdf", mimeType: "application/pdf", buffer: pdf },
      requirementId,
    },
  }));

  // Submitted through the screen: the bar, then the confirmation dialog.
  await page.goto(`/panel/applicant/applications/${draft.id}`);
  const dialog = page.getByRole("dialog");
  await onScreen(async () => {
    await page.getByRole("button", { name: "Złóż wniosek" }).first().click();
    await expect(dialog).toBeVisible({ timeout: 2_000 });
  });
  await dialog.getByRole("button", { name: "Złóż wniosek" }).click();
  await expect(page.getByText("Wniosek został złożony")).toBeVisible();

  const submitted = await json<{ number: string; status: string }>(await api.get(`${apiUrl}/applications/${draft.id}`));
  expect(submitted.status).toBe("Submitted");
  expect(await waitForMail(applicant.email, "Potwierdzenie złożenia oferty")).toContain(`Numer wniosku: ${submitted.number}`);

  return { id: draft.id, number: submitted.number, email: applicant.email, entityName, page };
}
