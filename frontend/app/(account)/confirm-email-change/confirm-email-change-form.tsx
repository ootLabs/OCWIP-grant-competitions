"use client";

import Link from "next/link";
import { useState } from "react";

import { accountSubmitClassName } from "@/components/account-field";
import { accountFailure, confirmEmailChange, loginPath } from "@/lib/account";

/**
 * Confirming a new address (T-106). A button, not a request on opening the
 * page: a mail client that fetches links ahead to preview them would
 * otherwise use the one time token before the person does.
 *
 * The address is not named on the screen, because the link no longer carries
 * it (obserwacja 2): it waits on the account until this confirmation. The
 * page is open in the mailbox the mail arrived in, so the person reading it
 * knows which address they are confirming.
 */
export function ConfirmEmailChangeForm({
  userId,
  token,
}: {
  userId: string | null;
  token: string | null;
}) {
  const [state, setState] = useState<"ready" | "sending" | "done">("ready");
  const [error, setError] = useState<string | null>(
    userId && token ? null : "Link jest niepełny. Otwórz go jeszcze raz z wiadomości.",
  );

  if (state === "done") {
    return (
      <p role="status">
        Adres konta został zmieniony na ten, na który przyszła wiadomość. Zaloguj się nim:{" "}
        <Link className="underline" href={loginPath}>
          logowanie
        </Link>
        .
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm">
        Potwierdzasz zmianę adresu konta na ten, na który przyszła ta wiadomość. Po potwierdzeniu
        wszystkie sesje zostaną wylogowane, a do logowania służy już nowy adres.
      </p>
      {error ? <p role="alert" className="text-sm">{error}</p> : null}
      <button
        type="button"
        disabled={!userId || !token || state === "sending"}
        className={accountSubmitClassName}
        onClick={async () => {
          setState("sending");
          setError(null);
          try {
            await confirmEmailChange(userId!, token!);
            setState("done");
          } catch (failure) {
            setError(accountFailure(failure).message);
            setState("ready");
          }
        }}
      >
        Potwierdź zmianę adresu
      </button>
    </div>
  );
}
