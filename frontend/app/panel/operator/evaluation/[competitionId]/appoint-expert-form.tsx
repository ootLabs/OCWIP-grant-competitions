"use client";

import { useId, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { appointExpert, needsName } from "@/lib/competition-experts";

/**
 * "Powołanie komisji" (R-44, report step 5.1): the operator types an
 * address. An existing account is appointed at once; an address with no
 * account asks for the person's name and sends an invitation instead.
 */
export function AppointExpertForm({ competitionId, onAppointed }: { competitionId: string; onAppointed: () => void }) {
  const [email, setEmail] = useState("");
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [inviting, setInviting] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ tone: "ok" | "error"; text: string } | null>(null);
  const ids = { email: useId(), first: useId(), last: useId() };

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setMessage(null);

    try {
      const expert = await appointExpert(
        competitionId,
        inviting ? { email, firstName, lastName } : { email },
      );
      setMessage({
        tone: "ok",
        text: expert.invitationPending
          ? `Wysłano zaproszenie na ${expert.email}. Osoba ustawi hasło z linku w wiadomości.`
          : `Powołano: ${expert.firstName} ${expert.lastName}. Dostanie wiadomość o powołaniu.`,
      });
      setEmail("");
      setFirstName("");
      setLastName("");
      setInviting(false);
      onAppointed();
    } catch (error) {
      if (!inviting && needsName(error)) {
        setInviting(true);
        setMessage({ tone: "error", text: "Nie ma konta z tym adresem. Podaj imię i nazwisko, a wyślemy zaproszenie." });
      } else {
        setMessage({ tone: "error", text: apiErrorMessage(error, "Nie udało się powołać eksperta. Spróbuj ponownie.") });
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="flex flex-col gap-3" onSubmit={submit} noValidate>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <label htmlFor={ids.email} className="flex flex-1 flex-col gap-1 text-sm">
          Adres e-mail eksperta
          <input
            id={ids.email}
            type="email"
            required
            autoComplete="off"
            className="rounded-sm border border-border-control px-2 py-1"
            value={email}
            onChange={(event) => {
              setEmail(event.target.value);
              setInviting(false);
            }}
          />
        </label>
        {inviting ? (
          <>
            <label htmlFor={ids.first} className="flex flex-col gap-1 text-sm">
              Imię
              <input
                id={ids.first}
                className="rounded-sm border border-border-control px-2 py-1"
                value={firstName}
                onChange={(event) => setFirstName(event.target.value)}
              />
            </label>
            <label htmlFor={ids.last} className="flex flex-col gap-1 text-sm">
              Nazwisko
              <input
                id={ids.last}
                className="rounded-sm border border-border-control px-2 py-1"
                value={lastName}
                onChange={(event) => setLastName(event.target.value)}
              />
            </label>
          </>
        ) : null}
        <button
          type="submit"
          className="rounded-sm border border-brand-accent px-3 py-1 text-sm text-brand-accent-text disabled:opacity-50"
          disabled={busy || email.trim() === ""}
        >
          {busy ? "Zapisywanie…" : inviting ? "Zaproś do komisji" : "Powołaj do komisji"}
        </button>
      </div>
      {message !== null ? (
        <p role={message.tone === "error" ? "alert" : "status"} className={message.tone === "error" ? "text-sm text-brand-accent-text" : "text-sm"}>
          {message.text}
        </p>
      ) : null}
    </form>
  );
}
