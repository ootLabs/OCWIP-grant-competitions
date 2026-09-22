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

/**
 * The same API seen from the Next.js server instead of from the browser.
 *
 * NEXT_PUBLIC_API_URL is an address for the browser, which reaches the API
 * through a published port on the host. A server component runs inside the
 * frontend container, where that address points at the container itself and
 * nothing answers on it, so rendering a public page on the server needs the
 * compose service name. Read through a function, not a module constant: only
 * the NEXT_PUBLIC_ prefix is inlined into the client bundle, so a constant
 * evaluated at import time would be undefined in the browser and would look
 * like a missing setting rather than what it is, a server only address.
 */
export function serverApiBaseUrl(): string {
  return process.env.API_SERVER_URL ?? apiBaseUrl;
}

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
    /**
     * The plain `ProblemDetails.detail` text, when the backend sent one and
     * wrote it deliberately for a person to read (for example, telling apart
     * two different reasons a request lands on the same status code). Null
     * for everything else: `message` stays the generic one on purpose
     * (see apiFetch), and a caller has to opt in to `detail` explicitly
     * rather than getting it by default.
     */
    readonly detail: string | null = null,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/**
 * A request, plus the one thing fetch itself has no room for: which copy of
 * the API to talk to. See serverApiBaseUrl.
 */
export type ApiRequestInit = RequestInit & { baseUrl?: string };

export async function apiFetch<T>(
  path: ApiPath,
  init: ApiRequestInit = {},
): Promise<T> {
  const { baseUrl = apiBaseUrl, ...request } = init;

  const response = await fetch(`${baseUrl}${path}`, {
    ...request,
    // The session is carried by an HttpOnly cookie, which the browser only
    // sends cross origin when it is asked to. See docs/architektura.md.
    credentials: "include",
    headers: { "Content-Type": "application/json", ...request.headers },
  });

  if (!response.ok) {
    // The message stays generic on purpose: a stack trace or a database error
    // shown to an applicant is both useless to them and a hint to an attacker.
    // Field errors and `detail` are the exception, and only from
    // problem+json, which the backend writes deliberately (docs/architektura.md).
    const problem = await readProblem(response);
    throw new ApiError(
      response.status,
      `Request to ${path} failed.`,
      problem.fieldErrors,
      problem.detail,
    );
  }

  // 202 and 204 answer with no body at all (POST /register is the first of
  // them), and response.json() throws on an empty one. Read the body once, as
  // text, rather than buffering every successful response twice.
  const body = await response.text();

  return (body === "" ? undefined : JSON.parse(body)) as T;
}

async function readProblem(
  response: Response,
): Promise<{ fieldErrors: FieldErrors; detail: string | null }> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("application/problem+json")) {
    return { fieldErrors: {}, detail: null };
  }

  try {
    const problem = (await response.json()) as ValidationProblemDetails;
    return { fieldErrors: problem.errors ?? {}, detail: problem.detail ?? null };
  } catch {
    // A body that says problem+json and is not one tells us nothing useful.
    return { fieldErrors: {}, detail: null };
  }
}
