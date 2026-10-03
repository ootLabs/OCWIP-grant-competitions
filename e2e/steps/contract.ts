import { expect, type Page } from "@playwright/test";

import { json, onScreen, type Submitted } from "../lib/api";
import { apiUrl } from "../lib/env";
import { plainSpaces, waitForMail } from "../lib/mailpit";

interface ContractField {
  readonly name: string;
  readonly system: boolean;
  readonly value: string | null;
}

interface Contract {
  readonly id: string;
  readonly status: string;
  readonly fields: readonly ContractField[];
}

/** Today in Poland, the day a contract is signed on (never in the future). */
const today = () => new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Warsaw" }).format(new Date());

/**
 * T-100b: the funded organisation's contract is drawn up and reaches the
 * applicant; the organisation then resigns before signing, the freed grant
 * goes to the group on the reserve list, and the group's contract, drawn up
 * on the 2026 template with the members from the application, is signed.
 */
export async function contractAndResignation(
  operatorPage: Page, competitionId: string, organisation: Submitted, group: Submitted,
): Promise<void> {
  const api = operatorPage.context().request;

  // Drawn up for the organisation and visible on the applicant's own screen.
  await json(await api.post(`${apiUrl}/applications/${organisation.id}/contract`));
  await organisation.page.goto(`/panel/applicant/applications/${organisation.id}`);
  await expect(organisation.page.getByText("Umowa jest przygotowana do podpisu.")).toBeVisible();
  await expect(organisation.page.getByRole("link", { name: "Pobierz umowę (PDF)" })).toBeVisible();

  // The organisation withdraws; the operator confirms it and funds the reserve on the evaluation page.
  await operatorPage.goto(`/panel/operator/evaluation/${competitionId}`);
  await onScreen(async () => {
    await operatorPage.getByRole("button", { name: `Potwierdź rezygnację ${organisation.number}` }).click({ timeout: 2_000 });
    // Through a confirmation, like every other irreversible step here: the
    // resignation frees the money, moves it to the reserve list and mails the
    // applicant (B-GUI-16). The dialog's own button carries no number.
    await operatorPage.getByRole("dialog").getByRole("button", { name: "Potwierdź rezygnację", exact: true })
      .click({ timeout: 2_000 });
    await expect(operatorPage.getByRole("button", { name: `Potwierdź rezygnację ${organisation.number}` })).toHaveCount(0, { timeout: 5_000 });
  });
  await waitForMail(organisation.email, `Rezygnacja z dotacji: wniosek ${organisation.number}`);

  await operatorPage.getByLabel("Kwota dotacji z listy rezerwowej").fill("5700");
  await operatorPage.getByRole("button", { name: `Przyznaj dofinansowanie ${group.number}` }).click();
  expect(plainSpaces(await waitForMail(group.email, `Dofinansowanie z listy rezerwowej: wniosek ${group.number}`)))
    .toContain("5700,00 zł");

  // The group's contract: every blank typed in, then the signing recorded on its page.
  const contract = await json<Contract>(await api.post(`${apiUrl}/applications/${group.id}/contract`));
  const values = Object.fromEntries(contract.fields.filter((field) => !field.system).map((field) => [field.name, `E2E ${field.name}`]));
  await json(await api.put(`${apiUrl}/contracts/${contract.id}/values`, { data: { values } }));

  await operatorPage.goto(`/panel/operator/evaluation/${competitionId}/${group.id}`);
  await onScreen(async () => {
    await operatorPage.getByLabel("Data podpisania").fill(today());
    await operatorPage.getByRole("button", { name: "Zapisz podpisanie umowy" }).click();
    await operatorPage.getByRole("dialog").getByRole("button", { name: "Zapisz podpisanie" }).click({ timeout: 2_000 });
    await expect(operatorPage.getByText(/Podpisana \d/)).toBeVisible({ timeout: 5_000 });
  });

  // The applicant sees it signed, with the group's members in it (P17).
  await group.page.goto(`/panel/applicant/applications/${group.id}`);
  // The contract section says it with the date; the status label says it too.
  await expect(group.page.getByText(/Umowa podpisana \d/)).toBeVisible();
  const signed = await json<Contract>(await group.page.context().request.get(`${apiUrl}/applications/${group.id}/contract`));
  expect(signed.status).toBe("Signed");
  expect(signed.fields.find((field) => field.name === "czlonkowie_grupy")?.value).toBe("Anna Testowa, Jan Testowy, Ewa Testowa");

  const applications = await json<{ applications: { number: string; status: string }[] }>(
    await api.get(`${apiUrl}/competitions/${competitionId}/applications`),
  );
  const status = (number: string) => applications.applications.find((item) => item.number === number)?.status;
  expect([status(organisation.number), status(group.number)]).toEqual(["Resigned", "ContractSigned"]);
}
