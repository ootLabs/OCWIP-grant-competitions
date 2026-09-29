import { expect, test } from "@playwright/test";

import { cspViolations, forgetCspViolations, newContext } from "../lib/api";

test.beforeEach(forgetCspViolations);

/**
 * T-112: the public pages under the Content Security Policy, with nothing
 * to set up, so the check also runs against the production compose file
 * behind Caddy, where the policy is at its strictest (no 'unsafe-eval').
 * A page whose scripts the policy refused would still render on the server;
 * the contrast switch proves the browser runs them.
 */
test("the public pages run under the policy", async ({ browser }) => {
  const page = await (await newContext(browser)).newPage();

  for (const path of ["/", "/competitions", "/archive", "/login", "/register", "/deklaracja-dostepnosci", "/regulamin", "/klauzula-informacyjna", "/kontakt"]) {
    const response = await page.goto(path);
    expect(response?.headers()["content-security-policy"], path).toContain("frame-ancestors 'none'");
    expect(response?.headers()["x-content-type-options"], path).toBe("nosniff");
  }

  await page.goto("/");
  await expect(async () => {
    await page.getByRole("button", { name: "Wysoki kontrast" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-contrast", "true", { timeout: 2_000 });
  }).toPass({ timeout: 30_000 });

  expect(cspViolations).toEqual([]);
});
