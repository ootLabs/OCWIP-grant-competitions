/**
 * Single entry point for talking to the .NET API.
 *
 * Everything goes through here so the session handling, the error format and
 * the base URL are decided once. The shapes come from lib/api-schema.ts, which
 * is generated from the backend's OpenAPI document by `npm run api:generate`
 * (card T-17). Nothing in this file is written by hand twice.
 */

import type { components, paths } from "./api-schema";

export const apiBaseUrl =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";

/** Every path the backend actually serves. A typo stops the build. */
export type ApiPath = keyof paths;

export type ProblemDetails = components["schemas"]["ProblemDetails"];
export type ValidationProblemDetails =
  components["schemas"]["HttpValidationProblemDetails"];

/** Field name to its messages, exactly as ProblemDetails carries them. */
export type FieldErrors = NonNullable<ValidationProblemDetails["errors"]>;

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    /**
     * Validation messages keyed by field, empty for every other failure.
     * These are written by the backend for the applicant to read, so a form
     * can show them next to the input they belong to.
     */
    readonly fieldErrors: FieldErrors = {},
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export async function apiFetch<T>(
  path: ApiPath,
  init: RequestInit = {},
): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    // The session is carried by an HttpOnly cookie, which the browser only
    // sends cross origin when it is asked to. See docs/architektura.md.
    credentials: "include",
    headers: { "Content-Type": "application/json", ...init.headers },
  });

  if (!response.ok) {
    // The message stays generic on purpose: a stack trace or a database error
    // shown to an applicant is both useless to them and a hint to an attacker.
    // Field errors are the one exception, and only from problem+json, which
    // the backend writes deliberately (docs/architektura.md).
    throw new ApiError(
      response.status,
      `Request to ${path} failed.`,
      await readFieldErrors(response),
    );
  }

  // 202 and 204 answer with no body at all (POST /register is the first of
  // them), and response.json() throws on an empty one.
  if (response.status === 204 || (await response.clone().text()) === "") {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function readFieldErrors(response: Response): Promise<FieldErrors> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("application/problem+json")) {
    return {};
  }

  try {
    const problem = (await response.json()) as ValidationProblemDetails;
    return problem.errors ?? {};
  } catch {
    // A body that says problem+json and is not one tells us nothing useful.
    return {};
  }
}
