"use client";

import Link from "next/link";
import { useState } from "react";

import {
  AccountField,
  accountSubmitClassName,
} from "@/components/account-field";
import {
  accountFailure,
  loginPath,
  passwordHint,
  resetPassword,
  type AccountFailure,
} from "@/lib/account";
import { ApiError } from "@/lib/api-client";
import { forgotPasswordPath } from "@/lib/login";

const incompleteLinkMessage =
  "Link jest niepełny. Otwórz go jeszcze raz z wiadomości albo poproś o nowy.";

/**
 * Setting a new password from the link in the reset mail (T-12.8).
 *
 * Two kinds of 400 need telling apart, and the backend already does: a
 * password the policy refused comes with fieldErrors.newPassword and the form
 * stays, a link that no longer works comes without them and the form goes,
 * because no password typed into it could succeed. The way on from there is a
 * new link, not a retry.
 */
export function ResetPasswordForm({
  userId,
  token,
}: {
  userId: string | null;
  token: string | null;
}) {
  const [newPassword, setNewPassword] = useState("");
  const [failure, setFailure] = useState<AccountFailure | null>(null);
  const [deadLink, setDeadLink] = useState<string | null>(
    userId === null || token === null ? incompleteLinkMessage : null,
  );
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting || userId === null || token === null) {
      return;
    }

    setSubmitting(true);
    setFailure(null);

    try {
      await resetPassword(userId, token, newPassword);
      setNewPassword("");
      setDone(true);
    } catch (error) {
      const next = accountFailure(error);
      const linkRefused =
        error instanceof ApiError &&
        error.status === 400 &&
        Object.keys(error.fieldErrors).length === 0;

      if (linkRefused) {
        setDeadLink(next.message);
      } else {
        setFailure(next);
      }
      if (next.fieldErrors.newPassword !== undefined) {
        setNewPassword("");
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (done) {
    return (
      <div className="flex flex-col gap-4" role="status">
        <p>
          Hasło zostało zmienione. Wszystkie wcześniejsze sesje zostały
          wylogowane.
        </p>
        <p>
          <Link href={loginPath}>Zaloguj się nowym hasłem</Link>
        </p>
      </div>
    );
  }

  if (deadLink !== null) {
    return (
      <div className="flex flex-col gap-4">
        <p className="text-sm text-brand-accent-text" role="alert">
          {deadLink}
        </p>
        <p className="text-sm">
          <Link href={forgotPasswordPath}>Poproś o nowy link</Link>
        </p>
      </div>
    );
  }

  return (
    <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
      <AccountField
        autoComplete="new-password"
        errors={failure?.fieldErrors.newPassword}
        hint={passwordHint}
        label="Nowe hasło"
        name="newPassword"
        onChange={setNewPassword}
        type="password"
        value={newPassword}
      />

      {failure !== null && (
        <p className="text-sm text-brand-accent-text" role="alert">
          {failure.message}
        </p>
      )}

      <button
        className={accountSubmitClassName}
        disabled={submitting}
        type="submit"
      >
        {submitting ? "Trwa zapisywanie" : "Ustaw nowe hasło"}
      </button>
    </form>
  );
}
