"use client";

import { useState } from "react";
import { ScreenFrame } from "@/components/ScreenFrame";
import { type BudgetRow, formatAmount, parseAmount, summarise } from "@/lib/budget";

const LIMIT = 9000;

// Starts over the limit on purpose, so the error state is visible without
// anyone having to type first.
const INITIAL_ROWS: BudgetRow[] = [
  { name: "Ławki parkowe, 2 sztuki", amount: "3200" },
  { name: "Sadzonki żywopłotu i ziemia", amount: "1850" },
  { name: "Narzędzia ogrodnicze", amount: "940" },
  { name: "Wynagrodzenie animatora, 2 spotkania", amount: "2800" },
  { name: "Materiały promocyjne", amount: "610" },
];

export function BudgetEditor() {
  const [rows, setRows] = useState(INITIAL_ROWS);
  const summary = summarise(rows, LIMIT);

  function handleAmountChange(index: number, amount: string) {
    setRows((current) =>
      current.map((row, position) => (position === index ? { ...row, amount } : row)),
    );
  }

  const largest = rows[summary.largestIndex];

  return (
    <ScreenFrame>
      <div className="flex flex-col gap-5 px-5 py-8 sm:px-7">
        <div className="flex flex-col gap-2.5">
          <span className="text-[13px] font-semibold">Krok 5 z 6</span>
          <h1 className="text-3xl leading-snug">Ile to będzie kosztować?</h1>
          <p className="text-base leading-relaxed text-text-link">
            Zmieńcie dowolną kwotę, a limit przeliczy się od razu. O przekroczeniu nie
            dowiecie się dopiero przy wysyłce.
          </p>
        </div>

        <div className="flex flex-col overflow-hidden rounded-[var(--radius-sm)] border border-border">
          <div className="flex items-center gap-4 border-b border-border bg-surface-muted px-4 py-2.5">
            <span className="grow text-[11px] font-semibold uppercase tracking-[0.06em] text-text-link">
              Pozycja budżetu
            </span>
            <span className="w-[150px] shrink-0 text-[11px] font-semibold uppercase tracking-[0.06em] text-text-link">
              Kwota w zł
            </span>
          </div>

          {rows.map((row, index) => {
            const flagged = summary.over && index === summary.largestIndex;
            return (
              <div
                key={row.name}
                className={`flex items-center gap-4 border-b border-border-muted px-4 py-3 ${
                  flagged ? "bg-surface-muted" : "bg-bg"
                }`}
              >
                <label className="grow text-[15px]" htmlFor={`budget-row-${index}`}>
                  {row.name}
                </label>
                <input
                  id={`budget-row-${index}`}
                  inputMode="numeric"
                  value={row.amount}
                  onChange={(event) => handleAmountChange(index, event.target.value)}
                  className={`w-[150px] shrink-0 rounded-[var(--radius-sm)] border px-3 py-2.5 text-right font-body text-[15px] font-semibold text-text ${
                    flagged ? "border-brand-accent" : "border-border"
                  }`}
                />
              </div>
            );
          })}

          <div className="flex items-center gap-4 bg-surface-muted px-4 py-3.5">
            <span className="grow text-[15px] font-semibold">Razem</span>
            <span
              className={`w-[150px] shrink-0 text-right text-[17px] font-semibold ${
                summary.over ? "text-brand-accent-text" : "text-text"
              }`}
            >
              {formatAmount(summary.total)} zł
            </span>
          </div>
        </div>

        <div className="flex flex-col gap-2">
          <div className="flex flex-wrap items-baseline gap-3">
            <span className="text-[13px] font-semibold">Wykorzystanie limitu</span>
            <span className="grow" />
            <span className="text-[13px] text-text-link">
              {summary.usedPercent} procent z {formatAmount(LIMIT)} zł
            </span>
          </div>
          <span className="block h-2.5 overflow-hidden rounded-[var(--radius-pill)] bg-border-muted">
            <span
              className={`block h-2.5 rounded-[var(--radius-pill)] ${
                summary.over ? "bg-brand-accent" : "bg-brand-accent-hover"
              }`}
              style={{ width: `${Math.min(summary.usedPercent, 100)}%` }}
            />
          </span>
        </div>

        {summary.over ? (
          <div className="flex flex-col gap-2 rounded-[var(--radius-sm)] border-2 border-brand-accent px-5 py-4.5">
            <span className="text-base font-semibold">
              Budżet przekracza limit o {formatAmount(summary.excess)} zł
            </span>
            <span className="text-[15px] leading-relaxed text-text-link">
              Pozycja „{largest.name}" to {formatAmount(parseAmount(largest.amount))} zł i
              jest największa w tym zestawieniu. Zmniejszcie ją albo rozłóżcie różnicę na
              pozostałe pozycje.
            </span>
            <span className="text-[13px] leading-relaxed text-text-link">
              Dopóki budżet przekracza limit, przycisk złożenia oferty pozostaje
              nieaktywny.
            </span>
          </div>
        ) : (
          <div className="flex flex-col gap-1.5 rounded-[var(--radius-sm)] bg-surface-muted px-5 py-4.5">
            <span className="text-base font-semibold">
              {summary.remaining === 0
                ? "Budżet wykorzystuje limit co do złotówki"
                : `Mieści się w limicie, zostało ${formatAmount(summary.remaining)} zł`}
            </span>
            <span className="text-[15px] leading-relaxed text-text-link">
              Możecie przejść dalej. Kwoty da się zmieniać do momentu złożenia oferty.
            </span>
          </div>
        )}

        <div className="flex flex-wrap items-center gap-3">
          <span
            className={`inline-flex min-h-[48px] items-center justify-center rounded-[var(--radius-sm)] border px-6 text-[15px] font-semibold ${
              summary.over
                ? "border-border bg-surface-muted text-text-link"
                : "border-brand-accent bg-brand-accent text-bg"
            }`}
          >
            {summary.over
              ? "Popraw budżet, żeby przejść dalej"
              : "Zapisz i przejdź do załączników"}
          </span>
          <span className="inline-flex min-h-[48px] items-center justify-center rounded-[var(--radius-sm)] border border-border px-5 text-[15px] font-semibold text-text-link">
            Wróć do odbiorców
          </span>
          <span className="grow" />
          <span className="text-[13px] text-text-link">
            Limit w tym konkursie: {formatAmount(LIMIT)} zł
          </span>
        </div>
      </div>
    </ScreenFrame>
  );
}
