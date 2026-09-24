/**
 * The account screens other than sign in (T-12.8): registration, confirming
 * the address, asking for a password reset and setting the new password.
 *
 * Every call here answers the same way for an address that has an account and
 * one that does not (security rule 3 in AGENTS.md). The backend makes sure of
 * that, and the screens must not undo it: a success screen never names the
 * address it was given, and there is no branch that could tell the two apart.
 */

import { ApiError, apiFetch, type FieldErrors } from "./api-client";
import type { components } from "./api-schema";

export type RegisterRequest = components["schemas"]["RegisterRequest"];

export const loginPath = "/login";

/** Shown with fieldErrors, so the form says once that something is wrong. */
export const fixFieldsMessage = "Popraw zaznaczone pola.";
const tooManyAttemptsMessage =
  "Zbyt wiele prób. Spróbuj ponownie za kilka minut.";
export const unavailableMessage =
  "Nie udało się teraz połączyć z serwerem. Spróbuj ponownie za chwilę.";

/**
 * What the backend says about the password policy, repeated as a hint under
 * the input so nobody has to fail once to learn it. The refusals themselves
 * still come from the backend (CustomPasswordErrorConfiguration.cs).
 */
export const passwordHint =
  "Co najmniej 8 znaków, w tym wielka i mała litera, cyfra i znak specjalny.";

/**
 * Creates the account. 202 with no body for a free and a taken address alike;
 * the returnUrl goes into the verification mail when the backend accepts it.
 */
export async function register(request: RegisterRequest): Promise<void> {
  await apiFetch<void>("/register", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

/** Confirms the address with the userId and token from the mail. */
export async function verifyEmail(userId: string, token: string): Promise<void> {
  await apiFetch<void>("/verify-email", {
    method: "POST",
    body: JSON.stringify({ userId, token }),
  });
}

/** Always 200: an unknown, a confirmed and a throttled address look alike. */
export async function resendVerification(
  email: string,
  returnUrl: string | null,
): Promise<void> {
  await apiFetch<void>("/resend-verification", {
    method: "POST",
    body: JSON.stringify({ email, returnUrl }),
  });
}

/** Always 200, whether or not the address has an account. */
export async function forgotPassword(email: string): Promise<void> {
  await apiFetch<void>("/forgot-password", {
    method: "POST",
    body: JSON.stringify({ email }),
  });
}

/** Sets the new password with the userId and token from the reset mail. */
export async function resetPassword(
  userId: string,
  token: string,
  newPassword: string,
): Promise<void> {
  await apiFetch<void>("/reset-password", {
    method: "POST",
    body: JSON.stringify({ userId, token, newPassword }),
  });
}

export type AccountFailure = {
  /** One sentence for the form's alert. */
  message: string;
  /** The backend's messages per field, empty when the problem is not a field. */
  fieldErrors: FieldErrors;
  /**
   * The server looked at the request and said no, as opposed to never
   * answering. Only then does a form clear its password: after a dropped
   * connection the same password is what the retry needs.
   */
  refused: boolean;
};

/**
 * What an account screen shows for a failed call.
 *
 * 400 with field errors points at the fields. 400 without them, and 429, show
 * the backend's own sentence (a dead link, the rate limit), which it writes
 * for the user on purpose. Everything else, a 5xx and a network that never
 * answered included, is one "try again": an outage must not look like the
 * applicant's mistake, and must not show anything technical either.
 */
export function accountFailure(error: unknown): AccountFailure {
  if (error instanceof ApiError && error.status === 400) {
    const hasFieldErrors = Object.keys(error.fieldErrors).length > 0;

    return {
      message: hasFieldErrors
        ? fixFieldsMessage
        : (error.detail ?? fixFieldsMessage),
      fieldErrors: error.fieldErrors,
      refused: true,
    };
  }

  if (error instanceof ApiError && error.status === 429) {
    return {
      message: error.detail ?? tooManyAttemptsMessage,
      fieldErrors: {},
      refused: true,
    };
  }

  return { message: unavailableMessage, fieldErrors: {}, refused: false };
}
