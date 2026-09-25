"use client";

import { useEffect, useId, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { decideDeclaration, fetchDeclaration, type Declaration } from "@/lib/reviewer-work";

/**
 * The impartiality declaration in place of the applications of a competition
 * (T-40a): until it is accepted the expert sees how many applications wait,
 * never what they are. Accepting opens them; refusing asks for a reason and
 * excludes the expert, and neither is undone with a click.
 */
export function DeclarationBox({
  competitionId,
  assignedCount,
  onDecided,
}: {
  competitionId: string;
  assignedCount: number;
  onDecided: () => void;
}) {
  const [declaration, setDeclaration] = useState<Declaration | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [refusing, setRefusing] = useState(false);
  const [reason, setReason] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const reasonId = useId();

  useEffect(() => {
    let current = true;
    fetchDeclaration(competitionId)
      .then((value) => {
        if (current) setDeclaration(value);
      })
      .catch(() => {
        if (current) setLoadFailed(true);
      });
    return () => {
      current = false;
    };
  }, [competitionId]);

  async function decide(accept: boolean) {
    setSending(true);
    setError(null);
    try {
      setDeclaration(await decideDeclaration(competitionId, accept, accept ? null : reason));
      onDecided();
    } catch (failure) {
      setError(apiErrorMessage(failure, "Nie udało się zapisać decyzji."));
    } finally {
      setSending(false);
    }
  }

  if (loadFailed) {
    return <p className="text-sm">Nie udało się pobrać deklaracji bezstronności. Odśwież stronę.</p>;
  }

  if (declaration === null) {
    return <p className="text-sm">Wczytywanie deklaracji…</p>;
  }

  if (declaration.status === "Refused") {
    return (
      <p className="text-sm">
        Odmówiłeś złożenia deklaracji bezstronności w tym konkursie, więc nie oceniasz jego wniosków. Powód:{" "}
        {declaration.refusalReason}
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-3 rounded-sm border border-border-muted bg-surface-muted p-4">
      <p className="text-sm">
        Masz przydzielonych wniosków do oceny: {assignedCount}. Zobaczysz je po złożeniu deklaracji bezstronności.
      </p>
      <blockquote className="border-l-4 border-border-control pl-3 text-sm">{declaration.text}</blockquote>

      {refusing ? (
        <div className="flex flex-col gap-2">
          <label htmlFor={reasonId} className="text-sm font-medium">
            Powód odmowy
          </label>
          <textarea
            id={reasonId}
            className="rounded-sm border border-border-control px-2 py-1 text-sm"
            maxLength={1000}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />
          <div className="flex gap-3">
            <button
              type="button"
              className="rounded-sm border border-brand-accent px-3 py-1.5 text-sm text-brand-accent-text disabled:opacity-40"
              disabled={sending || reason.trim() === ""}
              onClick={() => void decide(false)}
            >
              Odmawiam złożenia deklaracji
            </button>
            <button type="button" className="text-sm underline" onClick={() => setRefusing(false)}>
              Wróć
            </button>
          </div>
        </div>
      ) : (
        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            className="rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover disabled:opacity-40"
            disabled={sending}
            onClick={() => void decide(true)}
          >
            Składam deklarację
          </button>
          <button type="button" className="text-sm underline" onClick={() => setRefusing(true)}>
            Nie mogę jej złożyć
          </button>
        </div>
      )}

      {error !== null ? (
        <p role="alert" className="text-sm text-brand-accent-text">
          {error}
        </p>
      ) : null}
    </div>
  );
}
