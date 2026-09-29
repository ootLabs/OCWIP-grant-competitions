import { expect, type APIRequestContext, type Page } from "@playwright/test";

import type { Person } from "./accounts";
import { run } from "./env";

/** Somebody of this run: "e2e-<role>-<run>@example.org". */
export const person = (role: string): Person => ({
  firstName: "Test",
  lastName: role,
  email: `e2e-${role.toLowerCase()}-${run}@example.org`,
});

/** A whole minute, the way the competition columns store it. */
export const minute = (offsetMs: number) => {
  const date = new Date(Date.now() + offsetMs);
  date.setUTCSeconds(0, 0);
  return date.toISOString();
};

/** The body of a successful answer; anything else fails the step with what the server said. */
export async function json<T>(response: Awaited<ReturnType<APIRequestContext["get"]>>): Promise<T> {
  expect(response.ok(), `${response.url()} answered ${response.status()}: ${await response.text()}`).toBe(true);
  return (await response.json()) as T;
}

/** The smallest file the upload recognises as a PDF by its bytes. */
export const pdf = Buffer.from("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");

/**
 * A step on a screen, tried again until it holds: on a page the development
 * server is still compiling, the first click can land before React hydrates
 * it. Each try must end in a state a second try cannot repeat.
 */
export const onScreen = (step: () => Promise<void>) => expect(step).toPass({ timeout: 30_000 });

/** A submitted application and who submitted it; the applicant's browser stays open for the later steps. */
export interface Submitted {
  readonly id: string;
  readonly number: string;
  readonly email: string;
  readonly entityName: string;
  readonly page: Page;
}
