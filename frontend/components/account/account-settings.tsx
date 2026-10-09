"use client";

import { type FormEvent, useState } from "react";

import { AccountField, accountSubmitClassName } from "@/components/account-field";
import {
  accountFailure,
  changePassword,
  passwordHint,
  passwordMismatch,
  requestEmailChange,
  type AccountFailure,
} from "@/lib/account";

/**
 * "Moje konto" (T-106, R-08), the same for every role: a new password with
 * the current one, and a new address that changes only after the link sent
 * to it is opened. What the server answers is shown as it comes; for the
 * address it is the same whether or not the address is taken.
 */
export function AccountSettings() {
  return (
    <div className="flex flex-col gap-8">
      <PasswordForm />
      <EmailForm />
    </div>
  );
}

function PasswordForm() {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  // The same repeat as registration and /reset-password (B-GUI-07 fixed only
  // the latter, P4-10): a typo in the new password otherwise leaves the reset
  // link as the only way back in. Checked from the first submit on, at every
  // change after that.
  const [nextRepeat, setNextRepeat] = useState("");
  const [repeatChecked, setRepeatChecked] = useState(false);
  const [failure, setFailure] = useState<AccountFailure | null>(null);
  const [done, setDone] = useState(false);
  const [sending, setSending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setRepeatChecked(true);
    if (next !== nextRepeat) {
      setDone(false);
      return;
    }

    setSending(true);
    setFailure(null);
    setDone(false);
    try {
      await changePassword(current, next);
      setCurrent("");
      setNext("");
      setNextRepeat("");
      setRepeatChecked(false);
      setDone(true);
    } catch (error) {
      setFailure(accountFailure(error));
    } finally {
      setSending(false);
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3" aria-labelledby="zmiana-hasla">
      <h2 id="zmiana-hasla" className="text-xl">
        Zmiana hasła
      </h2>
      <AccountField
        label="Obecne hasło"
        name="currentPassword"
        type="password"
        autoComplete="current-password"
        value={current}
        onChange={setCurrent}
        errors={failure?.fieldErrors.currentPassword}
      />
      <AccountField
        label="Nowe hasło"
        name="newPassword"
        type="password"
        autoComplete="new-password"
        value={next}
        onChange={setNext}
        errors={failure?.fieldErrors.newPassword}
        hint={passwordHint}
      />
      <AccountField
        label="Powtórz nowe hasło"
        name="newPasswordRepeat"
        type="password"
        autoComplete="new-password"
        value={nextRepeat}
        onChange={setNextRepeat}
        errors={repeatChecked && next !== nextRepeat ? [passwordMismatch] : undefined}
      />
      {failure ? <p role="alert" className="text-sm">{failure.message}</p> : null}
      {done ? (
        <p role="status" className="text-sm">
          Hasło zmienione. Inne urządzenia zostały wylogowane.
        </p>
      ) : null}
      <button type="submit" disabled={sending} className={accountSubmitClassName}>
        Zmień hasło
      </button>
    </form>
  );
}

function EmailForm() {
  const [address, setAddress] = useState("");
  const [password, setPassword] = useState("");
  const [failure, setFailure] = useState<AccountFailure | null>(null);
  const [done, setDone] = useState(false);
  const [sending, setSending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setSending(true);
    setFailure(null);
    setDone(false);
    try {
      await requestEmailChange(address, password);
      setPassword("");
      setDone(true);
    } catch (error) {
      setFailure(accountFailure(error));
    } finally {
      setSending(false);
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3" aria-labelledby="zmiana-adresu">
      <h2 id="zmiana-adresu" className="text-xl">
        Zmiana adresu e-mail
      </h2>
      <AccountField
        label="Nowy adres e-mail"
        name="newEmail"
        type="email"
        autoComplete="email"
        value={address}
        onChange={setAddress}
        errors={failure?.fieldErrors.newEmail}
      />
      <AccountField
        label="Obecne hasło"
        name="emailPassword"
        type="password"
        autoComplete="current-password"
        value={password}
        onChange={setPassword}
        errors={failure?.fieldErrors.currentPassword}
      />
      {failure ? <p role="alert" className="text-sm">{failure.message}</p> : null}
      {done ? (
        <p role="status" className="text-sm">
          Jeśli ten adres może zostać użyty, wysłaliśmy na niego link. Adres zmieni się dopiero po jego otwarciu, do
          tego czasu logujesz się dotychczasowym.
        </p>
      ) : null}
      <button type="submit" disabled={sending} className={accountSubmitClassName}>
        Zmień adres
      </button>
    </form>
  );
}
