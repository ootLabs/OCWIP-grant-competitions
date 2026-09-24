"use client";

import { useState } from "react";

import {
  AccountField,
  accountSubmitClassName,
} from "@/components/account-field";
import { accountFailure, resendVerification } from "@/lib/account";

/**
 * Asking for a new verification link after the old one did not work.
 *
 * The answer is one sentence for every address, because the backend's is:
 * unknown, already confirmed and still in the resend cooldown all get 200.
 */
export function ResendVerificationForm({
  returnUrl,
}: {
  returnUrl: string | null;
}) {
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
      await resendVerification(email, returnUrl);
      setSent(true);
    } catch (error) {
      setFailure(accountFailure(error).message);
    } finally {
      setSubmitting(false);
    }
  }

  if (sent) {
    return (
      <p role="status">
        Jeśli ten adres czeka na potwierdzenie, wysłaliśmy na niego nowy link.
        Kolejny można zamówić dopiero po kilku minutach.
      </p>
    );
  }

  return (
    <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
      <h2 className="text-base">Wyślij nowy link</h2>
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
        {submitting ? "Trwa wysyłanie" : "Wyślij nowy link"}
      </button>
    </form>
  );
}
