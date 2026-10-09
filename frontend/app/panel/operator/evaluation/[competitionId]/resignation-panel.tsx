"use client";

import { useCallback, useEffect, useState } from "react";

import { ConfirmDialog } from "@/components/confirm-dialog";
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
  // Recording a resignation frees the money, moves it to the next application
  // on the reserve list and mails the applicant that they resigned. Every
  // other irreversible step in this product asks first; this one went on a
  // single click, and "Potwierdź" in the label is about the resignation the
  // applicant phoned in, not about the operator's own click.
  // The number is nullable on the wire, although a funded application always
  // has one: the dialog says so in words rather than printing nothing.
  const [resigning, setResigning] = useState<{ applicationId: string; number: string | null } | null>(null);

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

  /** False when it failed, so a dialog waiting on it stays open with the reason. */
  async function act(action: () => Promise<ResignationAction>, failureText: string): Promise<boolean> {
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
      return true;
    } catch (failure) {
      setError(apiErrorMessage(failure, failureText));
      return false;
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

      {/* Not while the dialog is open: it carries the refusal itself, and two
          live regions with one message read it out twice. */}
      {error !== null && resigning === null ? (
        <p role="alert" className="text-brand-accent-text">
          {error}
        </p>
      ) : null}
      {notice ? <p role="status">{notice}</p> : null}

      {state.unsigned.length === 0 ? (
        // True also when nothing is funded at all, e.g. after the only funded
        // application resigned (O-16): "all have a signed contract" was not.
        <p>Nie ma dofinansowanych wniosków bez podpisanej umowy.</p>
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
                onClick={() => {
                  // A message left over from an earlier action would read in
                  // the dialog as if this one had already failed.
                  setError(null);
                  setResigning({ applicationId: item.applicationId, number: item.number });
                }}
              >
                Potwierdź rezygnację {item.number}
              </button>
            </li>
          ))}
        </ul>
      )}

      {resigning !== null ? (
        <ConfirmDialog
          busy={busy}
          busyLabel="Zapisywanie…"
          confirmLabel="Potwierdź rezygnację"
          error={error}
          onCancel={() => setResigning(null)}
          // The dialog stays up while the request runs, so "Zapisywanie…" and
          // a refusal are where the operator is looking; it closes once the
          // resignation is recorded.
          onConfirm={() => {
            const { applicationId } = resigning;
            void act(
              () => confirmResignation(applicationId),
              "Nie udało się potwierdzić rezygnacji.",
            ).then((recorded) => {
              if (recorded) {
                setResigning(null);
              }
            });
          }}
          title={`Zapisać rezygnację wniosku ${resigning.number ?? "bez numeru"}? Wnioskodawca straci dofinansowanie, dostanie o tym wiadomość, a kwota wróci do puli dla listy rezerwowej.`}
        />
      ) : null}

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
