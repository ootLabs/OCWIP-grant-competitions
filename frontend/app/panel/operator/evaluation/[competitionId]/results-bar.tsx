"use client";

import { useEffect, useRef, useState } from "react";

import { primaryActionClassName } from "@/components/ui/styles";
import { formatAmount, formatMoment } from "@/lib/format";
import type { Ranking } from "@/lib/operator-evaluation";

/**
 * Always in view above the ranking list (T-42, report): how much is awarded
 * against the pool, over the pool in words as well as in colour, and the one
 * approval that publishes every result at once.
 */
export function ResultsBar({
  ranking,
  onApprove,
}: {
  ranking: Ranking;
  onApprove: () => Promise<string | null>;
}) {
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const awarded = Number(ranking.awardedTotal ?? 0);
  const pool = ranking.totalPool === null || ranking.totalPool === undefined ? null : Number(ranking.totalPool);
  const over = pool !== null && awarded > pool;

  async function approve() {
    setBusy(true);
    const failure = await onApprove();
    setBusy(false);
    setError(failure);
    if (failure === null) setConfirming(false);
  }

  return (
    <div className="flex flex-col gap-2 text-sm" aria-live="polite">
      <p className={over ? "font-semibold text-brand-accent-text" : "font-semibold"}>
        {pool === null
          ? `Przyznano ${formatAmount(awarded)}. Konkurs nie ma ustawionej puli.`
          : over
            ? `Przyznano ${formatAmount(awarded)} z ${formatAmount(pool)}: pula przekroczona o ${formatAmount(awarded - pool)}.`
            : `Przyznano ${formatAmount(awarded)} z ${formatAmount(pool)}, zostało ${formatAmount(pool - awarded)}.`}
      </p>
      {/* The same sentence as a bar, for the eye; a screen reader has the words. */}
      {pool === null || pool <= 0 ? null : (
        <span aria-hidden="true" className="block h-3 overflow-hidden rounded-pill bg-border-muted">
          <span
            className={`block h-full ${over ? "bg-status-negative-text" : "bg-brand-accent"}`}
            style={{ width: `${Math.min(100, Math.round((awarded / pool) * 100))}%` }}
          />
        </span>
      )}
      {ranking.resultsApprovedAt ? (
        <p>Wyniki zatwierdzono {formatMoment(ranking.resultsApprovedAt)}. Kwot nie można już zmieniać.</p>
      ) : (
        <div>
          <button
            type="button"
            className={primaryActionClassName}
            onClick={() => {
              setError(null);
              setConfirming(true);
            }}
          >
            Zatwierdź wyniki konkursu
          </button>
        </div>
      )}
      {confirming ? (
        <ConfirmDialog
          busy={busy}
          error={error}
          onCancel={() => setConfirming(false)}
          onConfirm={() => void approve()}
        />
      ) : null}
    </div>
  );
}

function ConfirmDialog({
  busy,
  error,
  onCancel,
  onConfirm,
}: {
  busy: boolean;
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
      aria-labelledby="confirm-results-title"
      onCancel={(event) => {
        event.preventDefault();
        onCancel();
      }}
      className="max-w-lg rounded-lg border border-border p-6 backdrop:bg-black/40"
    >
      <p id="confirm-results-title" className="text-lg">
        Zatwierdzić wyniki? Wnioski z kwotą zostaną dofinansowane, pozostałe powyżej progu trafią na listę
        rezerwową, reszta zostanie odrzucona. Wnioskodawcy zobaczą wynik, a kwot nie będzie można już zmienić.
      </p>
      {error !== null ? (
        <p role="alert" className="mt-2 text-sm text-brand-accent-text">
          {error}
        </p>
      ) : null}
      <div className="mt-4 flex justify-end gap-3">
        <button type="button" className="text-sm underline disabled:opacity-40" onClick={onCancel} disabled={busy}>
          Wróć
        </button>
        <button
          type="button"
          className={primaryActionClassName}
          onClick={onConfirm}
          disabled={busy}
        >
          {busy ? "Zatwierdzanie…" : "Zatwierdź"}
        </button>
      </div>
    </dialog>
  );
}
