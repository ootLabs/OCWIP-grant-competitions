"use client";

import { useState } from "react";

import { accountSubmitClassName } from "@/components/account-field";
import { requestEntityAccess } from "@/lib/access-requests";
import { apiErrorMessage } from "@/lib/api-client";

/**
 * "Ta organizacja jest już zarejestrowana" (T-93a, report step 2.2): the NIP
 * belongs to a card somebody founded, so instead of a second card the person
 * asks the founder to let them in. The founder hears about it by e-mail and,
 * after seven days without an answer, an operator of OCWIP.
 */
export function NipTaken({ nip, onRequested }: { nip: string; onRequested?: () => void }) {
  const [state, setState] = useState<"idle" | "sending" | "sent">("idle");
  const [failure, setFailure] = useState<string | null>(null);

  async function ask() {
    setState("sending");
    setFailure(null);

    try {
      await requestEntityAccess(nip);
      setState("sent");
      onRequested?.();
    } catch (error) {
      setFailure(apiErrorMessage(error, "Nie udało się wysłać prośby. Spróbuj ponownie."));
      setState("idle");
    }
  }

  if (state === "sent") {
    return (
      <div role="status" className="flex flex-col gap-2 border border-border-muted p-4 text-sm">
        <p className="text-base">Prośba o dostęp została wysłana.</p>
        <p>
          Rozpatrzy ją osoba, która założyła kartę tej organizacji. Dostaniesz e-mail z decyzją. Stan
          prośby widać w zakładce „Mój profil”.
        </p>
      </div>
    );
  }

  return (
    <div role="alert" className="flex flex-col gap-3 border border-border-muted p-4 text-sm">
      <p className="text-base">Ta organizacja jest już zarejestrowana.</p>
      <p>
        Karta z tym numerem NIP już istnieje, więc nie zakładamy drugiej. Poproś o dostęp: po
        zatwierdzeniu zobaczysz dane organizacji i jej wnioski, także robocze.
      </p>
      {failure !== null ? <p className="text-brand-accent-text">{failure}</p> : null}
      <div>
        <button type="button" className={accountSubmitClassName} disabled={state === "sending"} onClick={() => void ask()}>
          {state === "sending" ? "Wysyłanie…" : "Poproś o dostęp"}
        </button>
      </div>
    </div>
  );
}
