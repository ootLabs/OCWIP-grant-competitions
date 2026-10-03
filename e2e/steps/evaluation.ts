import { expect, type Browser, type Page } from "@playwright/test";

import { admin } from "../lib/admin";
import { registerAndVerify, signIn } from "../lib/accounts";
import { json, newContext, onScreen, person, type Submitted } from "../lib/api";
import { apiUrl } from "../lib/env";
import { plainSpaces, waitForMail } from "../lib/mailpit";
import type { ApplicantKind } from "../fixtures/answers-2026";

/** 44 points on each card for the organisation, 30 for the group: both pass the 2026 threshold of 50. */
const merit = (kind: ApplicantKind) =>
  kind === "Organisation"
    ? { pomysl_i_cel: 18, rezultaty: 14, promocja: 9, budzet: 3, proponowana_kwota: 5700, bez_wsparcia_nowefio: true }
    : { pomysl_i_cel: 12, rezultaty: 10, promocja: 6, budzet: 2, proponowana_kwota: 5700, grupa_z_patronem: false };

/**
 * The formal card by the operator, two experts from registration to a
 * finished merit card, the grant, the approval on the screen and the mails.
 * The organisation (88 points) gets the grant; the group (60 points, over the
 * threshold, no grant) goes to the reserve list, which T-100b draws on.
 */
export async function evaluate(
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
    const context = await newContext(browser);
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
    await onScreen(async () => {
      await page.getByRole("button", { name: "Składam deklarację" }).first().click();
      await expect(page.getByRole("button", { name: "Składam deklarację" })).toHaveCount(0, { timeout: 2_000 });
    });

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
    await onScreen(async () => {
      await operatorPage.getByRole("button", { name: move }).click();
      await operatorPage.getByRole("dialog").getByRole("button", { name: "Tak" }).click({ timeout: 2_000 });
      await expect(operatorPage.getByRole("button", { name: move })).toHaveCount(0, { timeout: 5_000 });
    });
  }

  // The approval and the result mails on the evaluation page.
  await operatorPage.goto(`/panel/operator/evaluation/${competitionId}`);
  await onScreen(async () => {
    await operatorPage.getByRole("button", { name: "Zatwierdź wyniki konkursu" }).click();
    await operatorPage.getByRole("dialog").getByRole("button", { name: "Zatwierdź" }).click({ timeout: 2_000 });
  });
  await expect(operatorPage.getByText(/Wyniki zatwierdzono/)).toBeVisible();
  await operatorPage.getByRole("button", { name: "Wyślij wiadomości o wynikach" }).click();

  expect(plainSpaces(await waitForMail(organisation.email, "Wynik konkursu: wniosek dofinansowany")))
    .toContain("Przyznana kwota: 5700,00 zł");
  expect(await waitForMail(group.email, "Wynik konkursu: wniosek na liście rezerwowej")).toContain(group.number);

  // The public list: the funded project with its grant, the reserve one without.
  const results = await (await newContext(browser)).newPage();
  await results.goto(`/competitions/${competitionId}/results`);
  const [funded, reserve] = await results.getByRole("table").all();
  await expect(funded!).toContainText(organisation.entityName);
  await expect(funded!).toContainText(/5\s?700,00/);
  await expect(reserve!).toContainText(group.entityName);
  await expect(reserve!).not.toContainText(/5\s?700,00/);
  await results.close();
}
