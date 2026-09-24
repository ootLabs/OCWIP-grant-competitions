"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { accountSubmitClassName } from "@/components/account-field";
import { accountFailure, loginPath, verifyEmail } from "@/lib/account";
import { withReturnUrl } from "@/lib/login";

import { ResendVerificationForm } from "./resend-verification-form";

const incompleteLinkMessage =
  "Link jest niepełny. Otwórz go jeszcze raz z wiadomości albo zamów nowy.";

type Outcome =
  | { state: "request" }
  | { state: "pending" }
  | { state: "confirmed" }
  | { state: "failed"; message: string; canRetry: boolean };

/**
 * Confirms the address as soon as the page opens (T-12.8).
 *
 * The request goes out once per page, guarded by a ref and not by state: in
 * development React runs an effect twice, and a second POST with the same
 * token answers 400 ("already confirmed"), which would replace a success the
 * visitor never saw with a failure.
 *
 * A failed confirmation still offers the way to sign in, because one reason
 * the backend gives for a 400 is an address confirmed already, for example
 * by clicking the same link a second time.
 *
 * Opened with no userId and no token at all, the page is not a broken link
 * but the way to ask for a new one: the screen after registration links here
 * for a mail that never arrived, which otherwise has no way back.
 */
export function VerifyEmail({
  userId,
  token,
  returnUrl,
}: {
  userId: string | null;
  token: string | null;
  returnUrl: string | null;
}) {
  const [outcome, setOutcome] = useState<Outcome>(() => {
    if (userId === null && token === null) {
      return { state: "request" };
    }
    if (userId === null || token === null) {
      return { state: "failed", message: incompleteLinkMessage, canRetry: false };
    }
    return { state: "pending" };
  });
  const started = useRef(false);

  const confirm = useCallback(async () => {
    if (userId === null || token === null) {
      return;
    }

    setOutcome({ state: "pending" });
    try {
      await verifyEmail(userId, token);
      setOutcome({ state: "confirmed" });
    } catch (error) {
      const failure = accountFailure(error);
      // Only a server that never answered is worth a second try with the
      // same link; a refused link stays refused.
      setOutcome({
        state: "failed",
        message: failure.message,
        canRetry: !failure.refused,
      });
    }
  }, [userId, token]);

  useEffect(() => {
    if (started.current) {
      return;
    }
    started.current = true;
    void confirm();
  }, [confirm]);

  const signIn = (
    <Link href={withReturnUrl(loginPath, returnUrl)}>Zaloguj się</Link>
  );

  if (outcome.state === "request") {
    return (
      <div className="flex flex-col gap-6">
        <p className="text-sm">
          Nie dotarła wiadomość z linkiem albo link wygasł? Podaj adres, na
          który założono konto, a wyślemy nowy.
        </p>
        <ResendVerificationForm returnUrl={returnUrl} />
      </div>
    );
  }

  if (outcome.state === "pending") {
    return <p role="status">Trwa potwierdzanie adresu.</p>;
  }

  if (outcome.state === "confirmed") {
    return (
      <div className="flex flex-col gap-4" role="status">
        <p>Adres e-mail jest potwierdzony. Możesz się teraz zalogować.</p>
        <p>{signIn}</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <p className="text-sm text-brand-accent-text" role="alert">
        {outcome.message}
      </p>

      {outcome.canRetry && (
        <button
          className={accountSubmitClassName}
          onClick={() => void confirm()}
          type="button"
        >
          Spróbuj ponownie
        </button>
      )}

      <p className="text-sm">Adres jest już potwierdzony? {signIn}</p>

      <ResendVerificationForm returnUrl={returnUrl} />
    </div>
  );
}
