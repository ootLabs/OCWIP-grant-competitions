"use client";

import Link from "next/link";
import { useState } from "react";

import {
  AccountField,
  accountSubmitClassName,
} from "@/components/account-field";
import {
  accountFailure,
  fixFieldsMessage,
  loginPath,
  passwordHint,
  register,
  verifyEmailPath,
  type AccountFailure,
  type ConsentDocument,
} from "@/lib/account";
import { withReturnUrl } from "@/lib/login";
import type { FieldErrors } from "@/lib/api-client";

const emailMismatch = "Adresy e-mail są różne. Sprawdź oba pola.";
const passwordMismatch = "Hasła są różne. Wpisz je jeszcze raz.";

/**
 * What the two repeated boxes caught, in the shape the backend uses for its
 * own refusals (T-124, R-19), so the form says once in its alert that
 * something needs fixing and each box carries its own reason.
 *
 * The address is compared WITHOUT case, because the unique index behind
 * registration stands on the normalised address: Biuro@ and biuro@ are one
 * account, so refusing that pair would be an alarm about a typo that is not
 * one. The fold is toLowerCase, NOT toLocaleLowerCase: the backend folds
 * invariantly (EmailNormalizer), and under a Turkish locale the locale fold
 * turns a dotless i into an i, so BIURO@ and bıuro@ would compare equal and
 * the form would wave through the very typo it is here to catch.
 * Whitespace is NOT trimmed away, because a trailing space really does
 * make a different address to the backend, and that is worth saying out loud.
 *
 * The password is compared exactly. Case and spaces are part of a password.
 */
function repeatMismatches(
  email: string,
  emailRepeat: string,
  password: string,
  passwordRepeat: string,
): FieldErrors {
  const problems: FieldErrors = {};

  if (email.toLowerCase() !== emailRepeat.toLowerCase()) {
    problems.emailRepeat = [emailMismatch];
  }

  if (password !== passwordRepeat) {
    problems.passwordRepeat = [passwordMismatch];
  }

  return problems;
}

/**
 * The registration form (T-12.8).
 *
 * The answer after sending is one screen for every accepted request, and it
 * does not repeat the address: the backend answers the same for a taken and a
 * free one, and a screen that said "we sent a link to x" would still be the
 * same screen, but one that looks like a promise the backend did not make.
 *
 * Each document in force (T-107) is shown in full with its own box to tick;
 * what goes back is the version of the text shown, so an acceptance always
 * names the words the person read.
 *
 * The address and the password are typed twice (T-124, R-19). Both repeats
 * live here and nowhere else: neither reaches the backend, because a typo in
 * an address costs the account a verification mail it will never read, and
 * that is a front end problem in a front end form. The pair is checked on
 * submit rather than on every keystroke, because while the second address is
 * still being typed "the addresses differ" is true and useless.
 */
export function RegisterForm({
  consents,
  returnUrl,
}: {
  consents: ConsentDocument[];
  returnUrl: string | null;
}) {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [emailRepeat, setEmailRepeat] = useState("");
  const [password, setPassword] = useState("");
  const [passwordRepeat, setPasswordRepeat] = useState("");
  const [failure, setFailure] = useState<AccountFailure | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [accepted, setAccepted] = useState(false);
  const [ticked, setTicked] = useState<string[]>([]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting) {
      return;
    }

    const mismatches = repeatMismatches(email, emailRepeat, password, passwordRepeat);
    if (Object.keys(mismatches).length > 0) {
      setFailure({ message: fixFieldsMessage, fieldErrors: mismatches, refused: true });
      return;
    }

    setSubmitting(true);
    setFailure(null);

    try {
      await register({
        email,
        password,
        firstName,
        lastName,
        returnUrl,
        acceptedConsents: ticked,
      });
      setPassword("");
      setPasswordRepeat("");
      setAccepted(true);
    } catch (error) {
      const next = accountFailure(error);
      setFailure(next);
      // Only a password the policy refused is cleared. A missing name or the
      // rate limit is no reason to make anybody type a good password again.
      if (next.fieldErrors.password !== undefined) {
        setPassword("");
        setPasswordRepeat("");
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
          autoComplete="email"
          errors={fieldErrors.emailRepeat}
          label="Powtórz adres e-mail"
          name="emailRepeat"
          onChange={setEmailRepeat}
          type="email"
          value={emailRepeat}
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
        <AccountField
          autoComplete="new-password"
          errors={fieldErrors.passwordRepeat}
          label="Powtórz hasło"
          name="passwordRepeat"
          onChange={setPasswordRepeat}
          type="password"
          value={passwordRepeat}
        />

        {consents.map((document) => (
          <div className="flex flex-col gap-2" key={document.kind}>
            <div
              aria-label={document.title}
              className="max-h-40 overflow-y-auto whitespace-pre-wrap rounded border border-border-muted p-3 text-sm"
              role="region"
              tabIndex={0}
            >
              {document.text}
            </div>
            <label className="flex items-start gap-2 text-sm">
              <input
                checked={ticked.includes(document.version)}
                name="acceptedConsents"
                onChange={(event) =>
                  setTicked((current) =>
                    event.target.checked
                      ? [...current, document.version]
                      : current.filter((version) => version !== document.version),
                  )
                }
                type="checkbox"
                value={document.version}
              />
              <span>Akceptuję: {document.title}</span>
            </label>
          </div>
        ))}
        {fieldErrors.acceptedConsents?.map((message) => (
          <p className="text-sm text-brand-accent-text" key={message}>
            {message}
          </p>
        ))}

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
