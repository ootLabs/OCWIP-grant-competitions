"use client";

import Link from "next/link";
import { useState } from "react";

import { accountSubmitClassName } from "@/components/account-field";
import { accountFailure, confirmEmailChange, loginPath } from "@/lib/account";

/**
 * Confirming a new address (T-106). A button, not a request on opening the
 * page: a mail client that fetches links ahead to preview them would
 * otherwise use the one time token before the person does.
 */
export function ConfirmEmailChangeForm({
  userId,
  email,
  token,
}: {
  userId: string | null;
  email: string | null;
  token: string | null;
}) {
  const [state, setState] = useState<"ready" | "sending" | "done">("ready");
  const [error, setError] = useState<string | null>(
    userId && email && token ? null : "Link jest niepełny. Otwórz go jeszcze raz z wiadomości.",
  );

  if (state === "done") {
    return (
      <p role="status">
        Adres zmieniony na {email}. Zaloguj się nim:{" "}
        <Link className="underline" href={loginPath}>
          logowanie
        </Link>
        .
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm">Nowy adres konta: {email}</p>
      {error ? <p role="alert" className="text-sm">{error}</p> : null}
      <button
        type="button"
        disabled={!userId || !email || !token || state === "sending"}
        className={accountSubmitClassName}
        onClick={async () => {
          setState("sending");
          setError(null);
          try {
            await confirmEmailChange(userId!, email!, token!);
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
