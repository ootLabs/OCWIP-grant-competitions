"use client";

import { useEffect, useRef, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { formatMoment } from "@/lib/format";
import { fetchCardSharing, shareCards } from "@/lib/operator-evaluation";

/**
 * Sharing the evaluation cards with the applicants (T-41b, report step 5.5):
 * one decision for the whole competition, confirmed, never taken back.
 * Applicants then see the finished cards of their own applications without
 * anything about who evaluated.
 */
export function CardSharing({ competitionId }: { competitionId: string }) {
  const [sharedAt, setSharedAt] = useState<string | null | undefined>(undefined);
  const [confirming, setConfirming] = useState(false);
  const [sharing, setSharing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let current = true;
    fetchCardSharing(competitionId)
      .then((result) => {
        if (current) setSharedAt(result.sharedAt ?? null);
      })
      .catch((failure: unknown) => {
        if (current) setError(apiErrorMessage(failure, "Nie udało się sprawdzić, czy karty są udostępnione."));
      });
    return () => {
      current = false;
    };
  }, [competitionId]);

  async function share() {
    setSharing(true);
    setError(null);
    try {
      setSharedAt((await shareCards(competitionId)).sharedAt ?? null);
      setConfirming(false);
    } catch (failure) {
      setError(apiErrorMessage(failure, "Nie udało się udostępnić kart."));
    } finally {
      setSharing(false);
    }
  }

  return (
    <div className="flex flex-col gap-2 text-sm">
      {sharedAt ? (
        <p>Karty oceny udostępniono wnioskodawcom {formatMoment(sharedAt)}. Tej decyzji nie można cofnąć.</p>
      ) : null}
      {sharedAt === null ? (
        <>
          <p>
            Wnioskodawcy nie widzą jeszcze kart oceny. Po udostępnieniu każdy zobaczy zakończone karty swojego
            wniosku i punktację, bez danych osób oceniających.
          </p>
          <div>
            <button
              type="button"
              className="rounded-sm bg-brand-accent px-4 py-2 text-bg hover:bg-brand-accent-hover"
              onClick={() => setConfirming(true)}
            >
              Udostępnij karty wnioskodawcom
            </button>
          </div>
        </>
      ) : null}
      {error !== null && !confirming ? <p role="alert">{error}</p> : null}
      {confirming ? (
        <ConfirmShareDialog
          sharing={sharing}
          error={error}
          onCancel={() => {
            setConfirming(false);
            setError(null);
          }}
          onConfirm={() => void share()}
        />
      ) : null}
    </div>
  );
}

/** Native dialog, like the other irreversible confirmations: focus trap and Escape come with it. */
function ConfirmShareDialog({
  sharing,
  error,
  onCancel,
  onConfirm,
}: {
  sharing: boolean;
  error: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    ref.current?.showModal?.();
  }, []);

  return (
    <dialog
      ref={ref}
      aria-labelledby="confirm-share-title"
      onCancel={(event) => {
        event.preventDefault();
        onCancel();
      }}
      className="rounded-sm border border-border p-6 backdrop:bg-black/40"
    >
      <p id="confirm-share-title" className="text-lg">
        Udostępnić karty oceny wszystkim wnioskodawcom tego konkursu? Tej decyzji nie można cofnąć.
      </p>
      {error !== null ? (
        <p role="alert" className="mt-2 text-sm text-brand-accent-text">
          {error}
        </p>
      ) : null}
      <div className="mt-4 flex justify-end gap-3">
        <button type="button" className="text-sm underline disabled:opacity-40" onClick={onCancel} disabled={sharing}>
          Wróć
        </button>
        <button
          type="button"
          className="rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover disabled:opacity-40"
          onClick={onConfirm}
          disabled={sharing}
        >
          {sharing ? "Udostępnianie…" : "Udostępnij"}
        </button>
      </div>
    </dialog>
  );
}
