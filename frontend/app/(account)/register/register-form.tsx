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
  register,
  verifyEmailPath,
  type AccountFailure,
} from "@/lib/account";
import { withReturnUrl } from "@/lib/login";

/**
 * The registration form (T-12.8).
 *
 * The answer after sending is one screen for every accepted request, and it
 * does not repeat the address: the backend answers the same for a taken and a
 * free one, and a screen that said "we sent a link to x" would still be the
 * same screen, but one that looks like a promise the backend did not make.
 */
export function RegisterForm({ returnUrl }: { returnUrl: string | null }) {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [failure, setFailure] = useState<AccountFailure | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [accepted, setAccepted] = useState(false);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting) {
      return;
    }

    setSubmitting(true);
    setFailure(null);

    try {
      await register({ email, password, firstName, lastName, returnUrl });
      setPassword("");
      setAccepted(true);
    } catch (error) {
      const next = accountFailure(error);
      setFailure(next);
      // Only a password the policy refused is cleared. A missing name or the
      // rate limit is no reason to make anybody type a good password again.
      if (next.fieldErrors.password !== undefined) {
        setPassword("");
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (accepted) {
    return (
      <div className="flex flex-col gap-4" role="status">
        <p>
          Sprawdź skrzynkę pocztową. Jeśli konto dla tego adresu można założyć,
          wysłaliśmy na niego link do potwierdzenia. Konto zadziała po
          kliknięciu w ten link.
        </p>
        <p className="text-sm">
          Nie widzisz wiadomości? Zajrzyj do folderu ze spamem albo{" "}
          <Link href={withReturnUrl(verifyEmailPath, returnUrl)} className="underline">
            wyślij link jeszcze raz
          </Link>
          .
        </p>
      </div>
    );
  }

  const fieldErrors = failure?.fieldErrors ?? {};

  return (
    <div className="flex flex-col gap-6">
      <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
        <AccountField
          autoComplete="given-name"
          errors={fieldErrors.firstName}
          label="Imię"
          name="firstName"
          onChange={setFirstName}
          type="text"
          value={firstName}
        />
        <AccountField
          autoComplete="family-name"
          errors={fieldErrors.lastName}
          label="Nazwisko"
          name="lastName"
          onChange={setLastName}
          type="text"
          value={lastName}
        />
        <AccountField
          autoComplete="email"
          errors={fieldErrors.email}
          label="Adres e-mail"
          name="email"
          onChange={setEmail}
          type="email"
          value={email}
        />
        <AccountField
          autoComplete="new-password"
          errors={fieldErrors.password}
          hint={passwordHint}
          label="Hasło"
          name="password"
          onChange={setPassword}
          type="password"
          value={password}
        />

        {failure !== null && (
          <p className="text-sm text-brand-accent-text" role="alert">
            {failure.message}
          </p>
        )}

        <button className={accountSubmitClassName} disabled={submitting} type="submit">
          {submitting ? "Trwa zakładanie konta" : "Załóż konto"}
        </button>
      </form>

      <p className="text-sm">
        Masz już konto?{" "}
        <Link href={withReturnUrl(loginPath, returnUrl)} className="underline">Zaloguj się</Link>
      </p>
    </div>
  );
}
