"use client";

import { useId, useState } from "react";

import { decideAsOperator, type EscalatedAccessRequest } from "@/lib/access-requests";
import { apiErrorMessage } from "@/lib/api-client";
import { formatMoment } from "@/lib/format";

/**
 * One overdue request with who asks, for which card and who founded it, so
 * both can be phoned. A decision needs a note on how the person was checked:
 * it stays in the card's history with the operator's name and the date.
 */
export function EscalatedRequest({ request, onDecided }: { request: EscalatedAccessRequest; onDecided: () => void }) {
  const [note, setNote] = useState("");
  const [failure, setFailure] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const noteId = useId();

  async function decide(approve: boolean) {
    if (note.trim() === "") {
      setFailure("Zapisz, jak sprawdzono osobę proszącą o dostęp.");
      return;
    }

    setBusy(true);
    setFailure(null);

    try {
      await decideAsOperator(request.id, approve, note.trim());
      onDecided();
    } catch (error) {
      setFailure(apiErrorMessage(error, "Nie udało się zapisać decyzji. Spróbuj ponownie."));
      setBusy(false);
    }
  }

  const founder =
    request.founderEmail !== null && request.founderEmail !== undefined
      ? `${request.founderFirstName ?? ""} ${request.founderLastName ?? ""}, ${request.founderEmail}`.trim()
      : "brak aktywnego konta osoby zakładającej";

  return (
    <li className="flex flex-col gap-3 border border-border-muted p-4 text-sm">
      <h2 className="text-base">
        {request.entityName}
        {request.nip ? `, NIP ${request.nip}` : ""}
      </h2>
      <dl className="grid grid-cols-1 gap-1 sm:grid-cols-[auto_1fr] sm:gap-x-4">
        <dt>Prosi o dostęp</dt>
        <dd>
          {request.requesterFirstName} {request.requesterLastName}, {request.requesterEmail}
        </dd>
        <dt>Osoba, która założyła kartę</dt>
        <dd>{founder}</dd>
        <dt>Prośba z</dt>
        <dd>{formatMoment(request.requestedAt)}</dd>
      </dl>

      <label htmlFor={noteId} className="flex flex-col gap-1">
        Jak sprawdzono osobę proszącą
        <textarea
          id={noteId}
          className="border border-border-muted p-2"
          rows={2}
          maxLength={1000}
          value={note}
          onChange={(event) => setNote(event.target.value)}
        />
      </label>

      {failure !== null ? (
        <p role="alert" className="text-brand-accent-text">
          {failure}
        </p>
      ) : null}

      <div className="flex gap-4">
        <button type="button" className="underline" disabled={busy} onClick={() => void decide(true)}>
          Zatwierdź dostęp
        </button>
        <button type="button" className="underline" disabled={busy} onClick={() => void decide(false)}>
          Odrzuć prośbę
        </button>
      </div>
    </li>
  );
}
