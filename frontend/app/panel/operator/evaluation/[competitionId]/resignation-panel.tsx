"use client";

import { useCallback, useEffect, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { formatAmount, formatMoment } from "@/lib/format";
import {
  confirmResignation,
  fetchResignations,
  promoteFromReserve,
  type ResignationAction,
  type Resignations,
} from "@/lib/resignations";

/**
 * After the results (T-109): the funded applications still without a signed
 * contract, marked once their 14 days have passed (from the publication, or
 * from the promotion for one funded from the reserve list later), the
 * operator's confirmation of a resignation, and the reserve application the
 * system proposes for the freed money, with an amount the operator may change
 * within what is left of the pool.
 */
export function ResignationPanel({ competitionId, onChange }: { competitionId: string; onChange: () => void }) {
  const [state, setState] = useState<Resignations | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [amount, setAmount] = useState("");
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const loaded = await fetchResignations(competitionId);
      setState(loaded);
      setAmount(loaded.nextReserve?.proposedGrant != null ? String(loaded.nextReserve.proposedGrant) : "");
    } catch (failure) {
      setError(apiErrorMessage(failure, "Nie udało się pobrać stanu umów."));
    }
  }, [competitionId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function act(action: () => Promise<ResignationAction>, failureText: string) {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const done = await action();
      if (!done.mailSent) {
        setNotice("Zmiana jest zapisana, ale mail do wnioskodawcy nie wyszedł. Powiadom go inną drogą.");
      }
      await load();
      onChange();
    } catch (failure) {
      setError(apiErrorMessage(failure, failureText));
    } finally {
      setBusy(false);
    }
  }

  if (state === null) {
    return error ? <p className="text-sm">{error}</p> : <p className="text-sm">Wczytywanie…</p>;
  }

  const next = state.nextReserve;

  return (
    <div className="flex flex-col gap-3 text-sm">
      {state.contractDeadline ? (
        <p>Termin podpisania umów: {formatMoment(state.contractDeadline)} (14 dni od ogłoszenia wyników).</p>
      ) : null}
      {state.freePool != null ? <p>Wolne środki w puli: {formatAmount(state.freePool)}</p> : null}

      {error ? (
        <p role="alert" className="text-brand-accent-text">
          {error}
        </p>
      ) : null}
      {notice ? <p role="status">{notice}</p> : null}

      {state.unsigned.length === 0 ? (
        <p>Wszystkie dofinansowane wnioski mają podpisaną umowę.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {state.unsigned.map((item) => (
            <li key={item.applicationId} className="flex flex-wrap items-center gap-3">
              <span>
                {item.number}: {item.entityName}
                {item.awardedGrant != null ? `, ${formatAmount(item.awardedGrant)}` : ""}
                {item.overdue
                  ? " (termin minął)"
                  : item.deadline !== state.contractDeadline
                    ? ` (z listy rezerwowej, termin ${formatMoment(item.deadline)})`
                    : ""}
              </span>
              <button
                type="button"
                disabled={busy}
                className="rounded-sm border border-border-control px-2 py-1"
                onClick={() =>
                  act(() => confirmResignation(item.applicationId), "Nie udało się potwierdzić rezygnacji.")
                }
              >
                Potwierdź rezygnację {item.number}
              </button>
            </li>
          ))}
        </ul>
      )}

      {next ? (
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            void act(() => promoteFromReserve(next.applicationId, Number(amount)), "Nie udało się przyznać dofinansowania.");
          }}
        >
          <p className="basis-full">
            Następny na liście rezerwowej: {next.number}: {next.entityName}
            {next.requestedGrant != null ? `, wnioskowana kwota ${formatAmount(next.requestedGrant)}` : ""}.
          </p>
          <label className="flex flex-col gap-1">
            Kwota dotacji z listy rezerwowej
            <input
              type="number"
              min={0.01}
              step={0.01}
              className="rounded-sm border border-border-control px-2 py-1"
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
            />
          </label>
          <button type="submit" disabled={busy || amount === ""} className="rounded-sm border border-border-control px-3 py-1">
            Przyznaj dofinansowanie {next.number}
          </button>
        </form>
      ) : (
        <p>Na liście rezerwowej nie ma wniosków.</p>
      )}
    </div>
  );
}
