import { expect, test, type APIRequestContext, type Browser, type Page } from "@playwright/test";

import { admin } from "../lib/admin";
import { registerAndVerify, signIn, type Person } from "../lib/accounts";
import { apiUrl, run } from "../lib/env";
import { waitForMail } from "../lib/mailpit";
import { answers2026, type ApplicantKind } from "../fixtures/answers-2026";

/**
 * T-100 and T-100a: from an empty system to submitted applications and on to
 * the published results, the way people get there. Accounts through the registration form and the verification mail,
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

test("a competition goes from registration to published results", async ({ browser }) => {
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
  const organisation = await test.step("the organisation submits", () =>
    submit(browser, "Organisation", competition.id, published.attachments[0]!.id));
  const group = await test.step("the informal group submits", () =>
    submit(browser, "InformalGroup", competition.id, published.attachments[0]!.id));
  const numbers = [organisation.number, group.number];

  // The operator sees both, numbered in the order they came in.
  const list = await json<{ applications: { number: string; status: string }[] }>(
    await api.get(`${apiUrl}/competitions/${competition.id}/applications`),
  );
  expect(list.applications.map((item) => item.number).sort()).toEqual([...numbers].sort());
  expect(list.applications.every((item) => item.status === "Submitted")).toBe(true);

  await test.step("the evaluation, the approval and the results (T-100a)", () =>
    evaluate(browser, operatorPage, competition.id, organisation, group));

  await operatorContext.close();
});

interface Submitted {
  readonly id: string;
  readonly number: string;
  readonly email: string;
}

/** Five points short of the maximum on each card for the organisation, below the threshold for the group. */
const merit = (kind: ApplicantKind) =>
  kind === "Organisation"
    ? { pomysl_i_cel: 18, rezultaty: 14, promocja: 9, budzet: 3, proponowana_kwota: 5700, bez_wsparcia_nowefio: true }
    : { pomysl_i_cel: 10, rezultaty: 8, promocja: 4, budzet: 2, proponowana_kwota: 5700, grupa_z_patronem: false };

/**
 * The formal card by the operator, two experts from registration to a
 * finished merit card, the grant, the approval on the screen and the mails:
 * the organisation's 88 points pass the 2026 threshold of 50, the group's
 * 48 do not.
 */
async function evaluate(
  browser: Browser, operatorPage: Page, competitionId: string, organisation: Submitted, group: Submitted,
): Promise<void> {
  const api = operatorPage.context().request;

  await json(await api.put(`${apiUrl}/competitions/${competitionId}/evaluation-settings`, {
    data: { evaluatorsPerApplication: 2, scoreAggregation: "Sum", meritThreshold: 50, thresholdIncludesStrategic: false, divergenceThresholdPercent: null },
  }));

  for (const [application, kind] of [[organisation, "Organisation"], [group, "InformalGroup"]] as const) {
    const card = await json<{ id: string }>(await api.post(`${apiUrl}/applications/${application.id}/evaluations/formal`));
    const answers: Record<string, boolean> = {
      zlozony_w_terminie: true, uprawniony_wnioskodawca: true, siedziba_w_wojewodztwie: true,
      dzialania_w_wojewodztwie: true, dzialania_w_terminie: true, kwota_do_7000: true,
    };
    if (kind === "Organisation") {
      Object.assign(answers, { przychod_do_50000: true, rejestracja_do_60_miesiecy: true });
    }
    await json(await api.put(`${apiUrl}/evaluations/${card.id}`, { data: { answers } }));
    await json(await api.post(`${apiUrl}/evaluations/${card.id}/finish`));
  }

  // Two experts: ordinary accounts, the Reviewer role from the server command.
  const team = [person("Ekspert1"), person("Ekspert2")];
  for (const expert of team) {
    const context = await browser.newContext();
    const page = await context.newPage();
    await registerAndVerify(page, expert);
    admin("grant-role", "--email", expert.email, "--role", "Reviewer");

    const accounts = await json<{ id: string; email: string }[]>(await api.get(`${apiUrl}/accounts/team`));
    const reviewerId = accounts.find((account) => account.email === expert.email)!.id;
    for (const application of [organisation, group]) {
      await json(await api.post(`${apiUrl}/applications/${application.id}/assignments`, { data: { reviewerId } }));
    }

    // The impartiality declaration on the expert's own screen (T-40a).
    await signIn(page, expert.email);
    await page.goto("/panel/reviewer");
    await expect(async () => {
      await page.getByRole("button", { name: "Składam deklarację" }).first().click();
      await expect(page.getByRole("button", { name: "Składam deklarację" })).toHaveCount(0, { timeout: 2_000 });
    }).toPass({ timeout: 30_000 });

    for (const [application, kind] of [[organisation, "Organisation"], [group, "InformalGroup"]] as const) {
      const card = await json<{ id: string }>(await context.request.post(`${apiUrl}/applications/${application.id}/evaluations/merit`));
      const scores = merit(kind);
      await json(await context.request.put(`${apiUrl}/evaluations/${card.id}`, {
        data: {
          answers: {
            ...scores,
            pomysl_i_cel_uzasadnienie: "Uzasadnienie.", rezultaty_uzasadnienie: "Uzasadnienie.",
            promocja_uzasadnienie: "Uzasadnienie.", budzet_uzasadnienie: "Uzasadnienie.",
            biale_plamy: false,
          },
        },
      }));
      await json(await context.request.post(`${apiUrl}/evaluations/${card.id}/finish`));
    }
    await context.close();
  }

  await json(await api.put(`${apiUrl}/applications/${organisation.id}/grant-decision`, { data: { awardedGrant: 5700, reason: null } }));

  // Closing the intake and starting the review on the competition page.
  await operatorPage.goto(`/panel/operator/competitions/${competitionId}`);
  for (const move of ["Zamknij nabór", "Rozpocznij ocenę"]) {
    await expect(async () => {
      await operatorPage.getByRole("button", { name: move }).click();
      await operatorPage.getByRole("dialog").getByRole("button", { name: "Tak" }).click({ timeout: 2_000 });
      await expect(operatorPage.getByRole("button", { name: move })).toHaveCount(0, { timeout: 5_000 });
    }).toPass({ timeout: 30_000 });
  }

  // The approval and the result mails on the evaluation page.
  await operatorPage.goto(`/panel/operator/evaluation/${competitionId}`);
  await expect(async () => {
    await operatorPage.getByRole("button", { name: "Zatwierdź wyniki konkursu" }).click();
    await operatorPage.getByRole("dialog").getByRole("button", { name: "Zatwierdź" }).click({ timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
  await expect(operatorPage.getByText(/Wyniki zatwierdzono/)).toBeVisible();
  await operatorPage.getByRole("button", { name: "Wyślij wiadomości o wynikach" }).click();

  expect(await waitForMail(organisation.email, "Wynik konkursu: wniosek dofinansowany")).toContain("Przyznana kwota: 5700,00 zł");
  expect(await waitForMail(group.email, "Wynik konkursu: wniosek nie otrzymał dofinansowania")).toContain(group.number);

  // The public list: the funded project with its grant, the rejected one not at all.
  const results = await browser.newPage();
  await results.goto(`/competitions/${competitionId}/results`);
  const funded = results.getByRole("table").first();
  await expect(funded).toContainText(`Stowarzyszenie E2E ${run}`);
  await expect(funded).toContainText(/5\s?700,00/);
  await expect(results.getByText(`Sąsiedzi z Zaodrza ${run}`)).toHaveCount(0);
  await results.close();
}

/** Registers, fills the card and the form, uploads the statute and submits through the screen. */
async function submit(browser: Browser, kind: ApplicantKind, competitionId: string, requirementId: string): Promise<Submitted> {
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
  return { id: draft.id, number: submitted.number, email: applicant.email };
}
