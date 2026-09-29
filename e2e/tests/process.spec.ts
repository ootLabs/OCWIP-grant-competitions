import { expect, test } from "@playwright/test";

import { admin } from "../lib/admin";
import { registerAndVerify, signIn } from "../lib/accounts";
import { cspViolations, forgetCspViolations, json, minute, newContext, person } from "../lib/api";
import { apiUrl, run } from "../lib/env";
import { contractAndResignation } from "../steps/contract";
import { evaluate } from "../steps/evaluation";
import { submit } from "../steps/submission";

test.beforeEach(forgetCspViolations);

/**
 * T-100, T-100a and T-100b: from an empty system to submitted applications,
 * the published results and a signed contract, the way people get there.
 * Accounts through the registration form and the verification mail, the
 * operator's and the experts' roles and the competition's content through the
 * server commands from docs/wdrozenie.md, the rest through the API and the
 * screens. No SQL on the side: a step the product cannot do is a step this
 * test cannot do either. Each stage lives in steps/.
 */
test("a competition goes from registration to a signed contract", async ({ browser }) => {
  // The operator: an ordinary account, made an operator by the server command.
  const operator = person("Operator");
  const operatorContext = await newContext(browser);
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
        // One grant's worth: after the resignation it goes to the reserve list.
        totalPoolAmount: 7000,
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
    await api.get(`${apiUrl}/public/competitions/${competition.id}`),
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

  await test.step("the contract, the resignation and the reserve list (T-100b)", () =>
    contractAndResignation(operatorPage, competition.id, organisation, group));

  await Promise.all([organisation, group].map((applicant) => applicant.page.context().close()));

  // T-112: no screen of the whole process had anything refused by the policy.
  expect(cspViolations).toEqual([]);
  await operatorContext.close();
});
