import { expect, test, type APIRequestContext, type Browser } from "@playwright/test";

import { admin } from "../lib/admin";
import { registerAndVerify, signIn, type Person } from "../lib/accounts";
import { apiUrl, run } from "../lib/env";
import { waitForMail } from "../lib/mailpit";
import { answers2026, type ApplicantKind } from "../fixtures/answers-2026";

/**
 * T-100: from an empty system to submitted applications, the way people get
 * there. Accounts through the registration form and the verification mail,
 * the operator's role and the competition's content through the server
 * commands from docs/wdrozenie.md, the rest through the API and the screens.
 * No SQL on the side: a step the product cannot do is a step this test
 * cannot do either.
 */

const person = (role: string): Person => ({
  firstName: "Test",
  lastName: role,
  email: `e2e-${role.toLowerCase()}-${run}@example.org`,
});

/** A whole minute, the way the competition columns store it. */
const minute = (offsetMs: number) => {
  const date = new Date(Date.now() + offsetMs);
  date.setUTCSeconds(0, 0);
  return date.toISOString();
};

async function json<T>(response: Awaited<ReturnType<APIRequestContext["get"]>>): Promise<T> {
  expect(response.ok(), `${response.url()} answered ${response.status()}: ${await response.text()}`).toBe(true);
  return (await response.json()) as T;
}

/** The smallest file the upload recognises as a PDF by its bytes. */
const pdf = Buffer.from("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");

test("an applicant goes from registration to a submitted application", async ({ browser }) => {
  // The operator: an ordinary account, made an operator by the server command.
  const operator = person("Operator");
  const operatorContext = await browser.newContext();
  const operatorPage = await operatorContext.newPage();
  await registerAndVerify(operatorPage, operator);
  admin("grant-role", "--email", operator.email, "--role", "Operator");
  await signIn(operatorPage, operator.email);
  const api = operatorContext.request;

  // A competition that is open now: started an hour ago, closing in a week,
  // so the test never waits for a clock.
  const competition = await json<{ id: string }>(
    await api.post(`${apiUrl}/competitions`, {
      data: {
        number: `E2E/${run}`,
        title: `Konkurs E2E ${run}`,
        description: "Konkurs testu w przeglądarce (T-100).",
        startDate: minute(-60 * 60_000),
        endDate: minute(7 * 24 * 60 * 60_000),
        isContinuousIntake: false,
        maxGrantAmount: 7000,
        formDefinitionId: null,
        requiresPaperSubmission: false,
        maxIndirectCostPercent: 10,
        maxAverageAnnualRevenue: 50000,
        attachments: [{ title: "Statut", description: null, requirement: "Required", allowedFormats: ["Pdf"] }],
      },
    }),
  );

  const imported = admin(
    "import-content", "--competition", competition.id,
    "--application", "seed/forms/application-2026.json",
    "--formal", "seed/evaluation-cards/formal-2026.json",
    "--merit", "seed/evaluation-cards/merit-2026.json",
    "--report", "seed/forms/report-2026.json",
    "--contract", "seed/templates/contract-2026.txt",
  );
  expect(imported).toContain("Published the application form as version 1.");

  await json(await api.post(`${apiUrl}/competitions/${competition.id}/status`, { data: { status: "Published" } }));

  const published = await json<{ intake: { state: string }; attachments: { id: string }[] }>(
    await operatorContext.request.get(`${apiUrl}/public/competitions/${competition.id}`),
  );
  expect(published.intake.state).toBe("Open");

  // Two applicants, one of each kind, each in a browser of their own.
  const numbers: string[] = [];
  for (const kind of ["Organisation", "InformalGroup"] as const) {
    numbers.push(await submit(browser, kind, competition.id, published.attachments[0]!.id));
  }

  // The operator sees both, numbered in the order they came in.
  const list = await json<{ applications: { number: string; status: string }[] }>(
    await api.get(`${apiUrl}/competitions/${competition.id}/applications`),
  );
  expect(list.applications.map((item) => item.number).sort()).toEqual([...numbers].sort());
  expect(list.applications.every((item) => item.status === "Submitted")).toBe(true);

  await operatorContext.close();
});

/** Registers, fills the card and the form, uploads the statute and submits through the screen. */
async function submit(browser: Browser, kind: ApplicantKind, competitionId: string, requirementId: string): Promise<string> {
  const applicant = person(kind);
  const context = await browser.newContext();
  const page = await context.newPage();
  await registerAndVerify(page, applicant);
  await signIn(page, applicant.email);
  const api = context.request;

  // The public page offers the intake to somebody signed in.
  await page.goto(`/competitions/${competitionId}`);
  await expect(page.getByRole("heading", { level: 1 })).toContainText(`Konkurs E2E ${run}`);

  const card =
    kind === "Organisation"
      ? {
          type: kind,
          name: `Stowarzyszenie E2E ${run}`,
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
      : { type: kind, name: `Sąsiedzi z Zaodrza ${run}` };
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
  // Clicked again until the dialog opens: on a page the development server
  // is still compiling, the first click can land before React hydrates it.
  const dialog = page.getByRole("dialog");
  await expect(async () => {
    await page.getByRole("button", { name: "Złóż wniosek" }).first().click();
    await expect(dialog).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
  await dialog.getByRole("button", { name: "Złóż wniosek" }).click();
  await expect(page.getByText("Wniosek został złożony")).toBeVisible();

  const submitted = await json<{ number: string; status: string }>(await api.get(`${apiUrl}/applications/${draft.id}`));
  expect(submitted.status).toBe("Submitted");
  expect(await waitForMail(applicant.email, "Potwierdzenie złożenia oferty")).toContain(`Numer wniosku: ${submitted.number}`);

  await context.close();
  return submitted.number;
}
