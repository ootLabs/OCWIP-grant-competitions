"use client";

import { useRouter } from "next/navigation";
import { type FormEvent, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { localWarsawToUtcIso } from "@/lib/local-time";
import { copyCompetition, type OperatorCompetition } from "@/lib/operator-competitions";

/**
 * "Skopiuj konkurs" (T-98, R-11): the next edition from this one. The
 * operator gives what belongs to the new edition (number, title, dates); the
 * settings, lists, forms, cards, report form and contract template come
 * across as they are in force here, as new versions of the copy. The copy
 * opens as a draft on its own page.
 */
export function CopySection({ competition }: { competition: OperatorCompetition }) {
  const router = useRouter();
  const [number, setNumber] = useState("");
  const [title, setTitle] = useState(competition.title);
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setSending(true);
    setError(null);

    try {
      const copy = await copyCompetition(competition.id, {
        number,
        title,
        startDate: start ? localWarsawToUtcIso(start) : null,
        endDate: competition.isContinuousIntake || !end ? null : localWarsawToUtcIso(end),
        isContinuousIntake: competition.isContinuousIntake,
      });
      router.push(`/panel/operator/competitions/${copy.id}`);
    } catch (failure) {
      setError(apiErrorMessage(failure, "Nie udało się skopiować konkursu."));
      setSending(false);
    }
  }

  return (
    <section aria-labelledby="kopia" className="flex flex-col gap-2">
      <h2 id="kopia" className="text-xl">
        Skopiuj konkurs
      </h2>
      <p className="text-sm">
        Nowy konkurs dostaje ustawienia, listy załączników i kosztów, osoby kontaktowe, formularz wniosku, karty
        oceny, wzór sprawozdania i wzór umowy z tego konkursu. Nie dostaje dat, numeru ani niczego, co wydarzyło się
        w tym naborze.
      </p>
      <form onSubmit={submit} className="flex flex-col gap-2 text-sm">
        <label className="flex flex-col gap-1">
          Numer nowego konkursu
          <input
            className="rounded-sm border border-border-control px-2 py-1"
            value={number}
            onChange={(event) => setNumber(event.target.value)}
          />
        </label>
        <label className="flex flex-col gap-1">
          Nazwa nowego konkursu
          <input
            className="rounded-sm border border-border-control px-2 py-1"
            value={title}
            onChange={(event) => setTitle(event.target.value)}
          />
        </label>
        <label className="flex flex-col gap-1">
          Początek naboru
          <input
            type="datetime-local"
            step={60}
            className="rounded-sm border border-border-control px-2 py-1"
            value={start}
            onChange={(event) => setStart(event.target.value)}
          />
        </label>
        {competition.isContinuousIntake ? null : (
          <label className="flex flex-col gap-1">
            Koniec naboru
            <input
              type="datetime-local"
              step={60}
              className="rounded-sm border border-border-control px-2 py-1"
              value={end}
              onChange={(event) => setEnd(event.target.value)}
            />
          </label>
        )}
        {error ? <p role="alert">{error}</p> : null}
        <button type="submit" disabled={sending} className="self-start rounded-sm border border-border-control px-3 py-1">
          {sending ? "Kopiowanie…" : "Skopiuj konkurs"}
        </button>
      </form>
    </section>
  );
}
