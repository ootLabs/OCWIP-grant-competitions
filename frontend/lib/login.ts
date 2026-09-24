/**
 * Signing in from the front (T-12.7), and what to say when it does not work.
 *
 * Kept apart from session.ts, which answers "who is signed in" and nothing
 * else: that file is read by every panel on every visit, this one only by the
 * sign in screen.
 */

import { ApiError, apiFetch } from "./api-client";
import type { components } from "./api-schema";

export type LoginRequest = components["schemas"]["LoginRequest"];
export type LoginResponse = components["schemas"]["LoginResponse"];

/**
 * The account screens of T-12.8. Linked from the sign in screen already, so
 * until that card lands these two lead to the 404 page, on purpose: a missing
 * link would hide the way to reset a password, a dead one only delays it.
 */
export const registerPath = "/register";
export const forgotPasswordPath = "/forgot-password";

/*
 * Fallbacks for when a refusal arrives without the sentence the backend
 * normally writes (SessionEndpoints.cs). The first one is the same text on
 * purpose: the rule is one message for every credential failure, and a second
 * wording in the front would be a second, distinguishable answer.
 */
export const invalidCredentialsMessage = "Nieprawidłowy e-mail lub hasło.";
const emailNotConfirmedMessage =
  "Potwierdź swój adres e-mail, zanim się zalogujesz.";
const tooManyAttemptsMessage =
  "Zbyt wiele prób logowania. Spróbuj ponownie za kilka minut.";
export const unavailableMessage =
  "Nie udało się teraz zalogować. Spróbuj ponownie za chwilę.";

/**
 * Signs in and answers with where to go next.
 *
 * The returnUrl is handed to the backend as it came, unfiltered. Deciding
 * whether it is safe to follow is Services/LoginLandingPath.cs, which answers
 * with redirectPath: a second copy of that check here could only drift from
 * the one that has to exist anyway, and a front that builds its own redirect
 * target is how an open redirect gets back in.
 */
export async function login(
  email: string,
  password: string,
  returnUrl: string | null,
): Promise<LoginResponse> {
  const request: LoginRequest = { email, password, returnUrl };

  return apiFetch<LoginResponse>("/login", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

/**
 * Whether the server looked at the credentials and said no, as opposed to
 * never getting to answer. Only a refusal clears the password field: after a
 * dropped connection the same password is exactly what the retry needs.
 */
export function isRefusal(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.status === 400 ||
      error.status === 401 ||
      error.status === 403 ||
      error.status === 429)
  );
}

/**
 * The sentence the screen shows for a failed sign in.
 *
 * 401, 403 and 429 show the backend's own sentence, because it writes them
 * for the user on purpose: the lockout one carries the minutes left, and the
 * rate limiter's one is a different sentence on the same status. 400 gets the
 * credentials message rather than its validation detail, so a malformed
 * address is not an answer of its own. Everything else, 5xx and a network
 * that never answered included, gets one "try again later": a backend that is
 * down must not look like a wrong password.
 */
export function loginFailureMessage(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return unavailableMessage;
  }

  switch (error.status) {
    case 400:
      return invalidCredentialsMessage;
    case 401:
      return error.detail ?? invalidCredentialsMessage;
    case 403:
      return error.detail ?? emailNotConfirmedMessage;
    case 429:
      return error.detail ?? tooManyAttemptsMessage;
    default:
      return unavailableMessage;
  }
}

/**
 * A link to another account screen that keeps the way back to where the
 * visitor came from, so a detour through registration still ends on the
 * competition they meant to apply to.
 */
export function withReturnUrl(path: string, returnUrl: string | null): string {
  if (returnUrl === null || returnUrl === "") {
    return path;
  }

  return `${path}?${new URLSearchParams({ returnUrl }).toString()}`;
}
