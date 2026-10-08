/**
 * The account screens other than sign in (T-12.8): registration, confirming
 * the address, asking for a password reset and setting the new password.
 *
 * Every call here answers the same way for an address that has an account and
 * one that does not (security rule 3 in AGENTS.md). The backend makes sure of
 * that, and the screens must not undo it: a success screen never names the
 * address it was given, and there is no branch that could tell the two apart.
 */

import { ApiError, apiFetch, serverApiBaseUrl, type FieldErrors } from "./api-client";
import type { components } from "./api-schema";
import { humanCheckHeaders } from "./human-check";

export type RegisterRequest = components["schemas"]["RegisterRequest"];
export type ConsentDocument = components["schemas"]["ConsentDocument"];

export const loginPath = "/login";
export const verifyEmailPath = "/verify-email";

/** Two password boxes that differ, on registration and on a reset alike. */
export const passwordMismatch = "Hasła są różne. Wpisz je jeszcze raz.";

/** Shown with fieldErrors, so the form says once that something is wrong. */
export const fixFieldsMessage = "Popraw zaznaczone pola.";
/** A 400 that names no field and brings no sentence of its own. */
export const rejectedMessage =
  "Serwer nie przyjął tych danych. Sprawdź je i spróbuj ponownie.";
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
  "Co najmniej 12 znaków, w tym wielka i mała litera, cyfra i znak specjalny.";

/**
 * Creates the account. 202 with no body for a free and a taken address alike;
 * the returnUrl goes into the verification mail when the backend accepts it.
 */
export async function register(
  request: RegisterRequest,
  humanCheckToken: string | null = null,
): Promise<void> {
  await apiFetch<void>("/register", {
    method: "POST",
    headers: humanCheckHeaders(humanCheckToken),
    body: JSON.stringify(request),
  });
}

/**
 * The terms and the privacy notice in force (T-107), read on the server for
 * the registration page. Fresh every time: a version replaced since the page
 * was cached would be refused by /register, and the visitor would accept a
 * text they are no longer shown.
 */
/**
 * The document text without its markdown heading line.
 *
 * The backend reads a consent document straight from seed/consents/*.md and
 * serves the file as it is, first line included, because the version is the
 * hash of that whole text: strip the heading there and every acceptance
 * recorded so far stops matching. So it comes off here, at the point of
 * rendering, where the box showed a literal "# Regulamin serwisu" on the one
 * screen where somebody accepts a legal document. The title is already on
 * screen anyway, in the "Akceptuję: ..." label beside the checkbox.
 */
export function consentBody(text: string): string {
  return text.replace(/^#{1,6}[ \t]+.*\r?\n+/, "");
}

export async function fetchConsents(): Promise<ConsentDocument[]> {
  return apiFetch<ConsentDocument[]>("/public/consents", {
    cache: "no-store",
    baseUrl: serverApiBaseUrl(),
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
  humanCheckToken: string | null = null,
): Promise<void> {
  await apiFetch<void>("/resend-verification", {
    method: "POST",
    headers: humanCheckHeaders(humanCheckToken),
    body: JSON.stringify({ email, returnUrl }),
  });
}

/** Always 200, whether or not the address has an account. */
export async function forgotPassword(
  email: string,
  humanCheckToken: string | null = null,
): Promise<void> {
  await apiFetch<void>("/forgot-password", {
    method: "POST",
    headers: humanCheckHeaders(humanCheckToken),
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
   * answering: a dead link stays dead, while after a dropped connection the
   * same request is worth sending again.
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
        : (error.detail ?? rejectedMessage),
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

/** T-106: the signed in account's new password; the current one is required. */
export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  await apiFetch<void>("/me/password", {
    method: "POST",
    body: JSON.stringify({ currentPassword, newPassword }),
  });
}

/**
 * T-106: asks for a new address. The same answer whether or not the address
 * already has an account; the change happens only from the link in the mail.
 */
export async function requestEmailChange(newEmail: string, currentPassword: string): Promise<void> {
  await apiFetch<void>("/me/email", {
    method: "POST",
    body: JSON.stringify({ newEmail, currentPassword }),
  });
}

/**
 * T-106: the link from the mail to the new address. The address is not sent
 * and not in the link: the backend kept it on the account when the change was
 * requested, so it never reaches a browser history or a proxy log
 * (obserwacja 2).
 */
export async function confirmEmailChange(userId: string, token: string): Promise<void> {
  await apiFetch<void>("/confirm-email-change", {
    method: "POST",
    body: JSON.stringify({ userId, token }),
  });
}
