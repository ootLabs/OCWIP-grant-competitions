"use client";

import { useEffect, useId, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { formatMoment } from "@/lib/format";
import {
  fetchResultMessages,
  fetchResultNotifications,
  saveResultMessages,
  sendResultNotifications,
  type ResultMessages,
  type ResultNotifications,
} from "@/lib/operator-evaluation";

const fields = [
  { key: "funded", label: "Wniosek dofinansowany" },
  { key: "reserve", label: "Wniosek na liście rezerwowej" },
  { key: "rejected", label: "Wniosek bez dofinansowania" },
] as const;

/**
 * The result mails (T-43): OCWIP's text for each result, and after the
 * approval the sending, which can be run again safely. The system adds the
 * number, the competition and the amount under the text, so an empty field
 * still sends a complete mail.
 */
export function ResultMails({ competitionId, approved }: { competitionId: string; approved: boolean }) {
  const [texts, setTexts] = useState<Record<string, string>>({ funded: "", reserve: "", rejected: "" });
  const [state, setState] = useState<ResultNotifications | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    let current = true;
    fetchResultMessages(competitionId)
      .then((saved) => {
        if (current) setTexts({ funded: saved.funded ?? "", reserve: saved.reserve ?? "", rejected: saved.rejected ?? "" });
      })
      .catch(() => undefined);
    if (approved) {
      fetchResultNotifications(competitionId)
        .then((result) => {
          if (current) setState(result);
        })
        .catch(() => undefined);
    }
    return () => {
      current = false;
    };
  }, [competitionId, approved]);

  async function run(action: () => Promise<string>) {
    setBusy(true);
    try {
      setMessage(await action());
    } catch (failure) {
      setMessage(apiErrorMessage(failure, "Nie udało się wykonać operacji."));
    } finally {
      setBusy(false);
    }
  }

  const save = () =>
    run(async () => {
      const body: ResultMessages = { funded: texts.funded, reserve: texts.reserve, rejected: texts.rejected };
      await saveResultMessages(competitionId, body);
      return "Zapisano treści wiadomości.";
    });

  const send = () =>
    run(async () => {
      const result = await sendResultNotifications(competitionId);
      setState(result);
      return Number(result.failed) > 0
        ? "Część wiadomości nie wyszła. Wyślij ponownie: pójdą tylko te, których brakuje."
        : "Wysłano wszystkie wiadomości.";
    });

  return (
    <div className="flex flex-col gap-4 text-sm">
      {fields.map((field) => (
        <label key={field.key} htmlFor={`${id}-${field.key}`} className="flex flex-col gap-1">
          {field.label}
          <textarea
            id={`${id}-${field.key}`}
            rows={3}
            maxLength={4000}
            className="rounded-sm border border-border-control px-2 py-1"
            value={texts[field.key]}
            onChange={(event) => setTexts({ ...texts, [field.key]: event.target.value })}
          />
        </label>
      ))}
      <p>Puste pole wysyła zdanie domyślne. Numer wniosku, konkurs i przyznaną kwotę system dopisuje sam.</p>
      <div className="flex flex-wrap items-center gap-3">
        <button type="button" className="underline disabled:opacity-40" disabled={busy} onClick={() => void save()}>
          Zapisz treści
        </button>
        {approved ? (
          <button
            type="button"
            className="rounded-sm bg-brand-accent px-4 py-2 text-bg hover:bg-brand-accent-hover disabled:opacity-40"
            disabled={busy || (state !== null && Number(state.pending) === 0)}
            onClick={() => void send()}
          >
            {state !== null && Number(state.sent) > 0 ? "Wyślij brakujące wiadomości" : "Wyślij wiadomości o wynikach"}
          </button>
        ) : (
          <span>Wysyłka będzie możliwa po zatwierdzeniu wyników.</span>
        )}
      </div>
      {state !== null ? (
        <p>
          Wysłano {Number(state.sent)} z {Number(state.total)}
          {Number(state.failed) > 0 ? `, nie udało się: ${Number(state.failed)}` : ""}
          {state.lastSentAt ? `. Ostatnia wysyłka ${formatMoment(state.lastSentAt)}.` : "."}
        </p>
      ) : null}
      {message !== null ? <p role="status">{message}</p> : null}
    </div>
  );
}
