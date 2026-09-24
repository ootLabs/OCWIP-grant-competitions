"use client";

import Link from "next/link";
import { useState } from "react";

import {
  AccountField,
  accountSubmitClassName,
} from "@/components/account-field";
import { accountFailure, forgotPassword, loginPath } from "@/lib/account";

/**
 * Asking for a password reset link (T-12.8).
 *
 * One answer for every address, the backend's rule and this screen's: the
 * sentence after sending is the same whether or not an account exists, and it
 * does not repeat the address.
 */
export function ForgotPasswordForm() {
  const [email, setEmail] = useState("");
  const [failure, setFailure] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting) {
      return;
    }

    setSubmitting(true);
    setFailure(null);

    try {
      await forgotPassword(email);
      setSent(true);
    } catch (error) {
      setFailure(accountFailure(error).message);
    } finally {
      setSubmitting(false);
    }
  }

  const backToSignIn = (
    <p className="text-sm">
      <Link href={loginPath}>Wróć do logowania</Link>
    </p>
  );

  if (sent) {
    return (
      <div className="flex flex-col gap-4">
        <p role="status">
          Jeśli istnieje konto z tym adresem, wysłaliśmy na niego link do
          ustawienia nowego hasła. Jak długo link działa, podaje wiadomość.
        </p>
        {backToSignIn}
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
        <p className="text-sm">
          Podaj adres, na który założono konto. Wyślemy na niego link do
          ustawienia nowego hasła.
        </p>
        <AccountField
          autoComplete="email"
          label="Adres e-mail"
          name="email"
          onChange={setEmail}
          type="email"
          value={email}
        />

        {failure !== null && (
          <p className="text-sm text-brand-accent-text" role="alert">
            {failure}
          </p>
        )}

        <button
          className={accountSubmitClassName}
          disabled={submitting}
          type="submit"
        >
          {submitting ? "Trwa wysyłanie" : "Wyślij link"}
        </button>
      </form>

      {backToSignIn}
    </div>
  );
}
