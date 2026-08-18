/**
 * Single entry point for talking to the .NET API.
 *
 * Everything goes through here so the session handling, the error format and
 * the base URL are decided once. The typed client generated from OpenAPI
 * (card T-17) will replace the body of this module, not its callers.
 */

export const apiBaseUrl =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export async function apiFetch<T>(
  path: string,
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
    throw new ApiError(response.status, `Request to ${path} failed.`);
  }

  return (await response.json()) as T;
}
