import { expect, type Page } from "@playwright/test";

import { linkTo, waitForMail } from "./mailpit";

export const password = "E2e!Mocne-Haslo1";

export interface Person {
  readonly firstName: string;
  readonly lastName: string;
  readonly email: string;
}

/**
 * Registration through the form, both consents ticked (T-107), then the
 * link from the verification mail opened in the same browser.
 *
 * The address and the password are typed twice (T-124), and both are asked
 * for by their EXACT label: without exact, "Adres e-mail" also matches
 * "Powtórz adres e-mail" and the locator resolves to two boxes.
 */
export async function registerAndVerify(page: Page, person: Person): Promise<void> {
  await page.goto("/register");
  await page.getByLabel("Imię").fill(person.firstName);
  await page.getByLabel("Nazwisko").fill(person.lastName);
  await page.getByLabel("Adres e-mail", { exact: true }).fill(person.email);
  await page.getByLabel("Powtórz adres e-mail").fill(person.email);
  await page.getByLabel("Hasło", { exact: true }).fill(password);
  await page.getByLabel("Powtórz hasło").fill(password);
  for (const consent of await page.getByLabel(/^Akceptuję:/).all()) {
    await consent.check();
  }
  await page.getByRole("button", { name: "Załóż konto" }).click();
  await expect(page.getByRole("status")).toContainText("Sprawdź skrzynkę");

  const mail = await waitForMail(person.email, "Potwierdzenie adresu e-mail");
  await page.goto(linkTo(mail, "/verify-email"));
  await expect(page.getByText("Adres e-mail jest potwierdzony.")).toBeVisible();
}

export async function signIn(page: Page, email: string): Promise<void> {
  await page.goto("/login");
  await page.getByLabel("Adres e-mail").fill(email);
  await page.getByLabel("Hasło").fill(password);
  await page.getByRole("button", { name: "Zaloguj się" }).click();
  await expect(page).not.toHaveURL(/\/login/);
}
