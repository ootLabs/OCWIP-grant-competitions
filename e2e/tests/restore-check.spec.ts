import { expect, test } from "@playwright/test";

import { signIn } from "../lib/accounts";
import { json, newContext, person } from "../lib/api";
import { apiUrl } from "../lib/env";

/**
 * T-114: after scripts/restore.sh, the group of the same run (E2E_RUN) is
 * still there as a person meets it: it signs in, and its attachment, its
 * application PDF and its signed contract open. That proves the keys and the
 * files came back with the database, not only the rows. Runs only when
 * E2E_RESTORE_CHECK=1, after process.spec.ts, a backup and a restore.
 */
test.skip(process.env.E2E_RESTORE_CHECK !== "1", "only after a restore");

test("the restored system still has the group's application, files and contract", async ({ browser }) => {
  const group = person("InformalGroup");
  const page = await (await newContext(browser)).newPage();
  await signIn(page, group.email);
  const api = page.context().request;

  const applications = await json<{ id: string }[]>(await api.get(`${apiUrl}/applications`));
  const application = applications[0]!;

  const attachments = await json<{ id: string }[]>(await api.get(`${apiUrl}/applications/${application.id}/attachments`));
  const file = await api.get(`${apiUrl}/attachments/${attachments[0]!.id}`);
  expect(file.ok()).toBe(true);
  expect((await file.body()).subarray(0, 5).toString()).toBe("%PDF-");

  const pdf = await api.get(`${apiUrl}/applications/${application.id}/pdf`);
  expect(pdf.ok()).toBe(true);
  expect(pdf.headers()["content-type"]).toContain("application/pdf");

  const contract = await json<{ id: string; status: string }>(await api.get(`${apiUrl}/applications/${application.id}/contract`));
  expect(contract.status).toBe("Signed");
  expect((await api.get(`${apiUrl}/contracts/${contract.id}/pdf`)).ok()).toBe(true);
});
