"use client";

import { useState } from "react";

import {
  CompetitionAttachments,
  CompetitionContacts,
} from "@/app/competitions/competition-attachments";
import { CompetitionFacts } from "@/app/competitions/competition-facts";
import { statusLabels } from "@/app/competitions/labels";
import type { WizardStepId } from "@/lib/competition-wizard/types";
import type { OperatorCompetition } from "@/lib/operator-competitions";

/**
 * Krok 1.7: Podsumowanie (docs/runbook/pola.md), plus publikacja.
 *
 * "Podgląd przed publikacją pokazuje dokładnie to, co zobaczy wnioskodawca"
 * (kryterium karty): renderowany tymi samymi komponentami co publiczna strona
 * konkursu (CompetitionFacts, CompetitionAttachments, CompetitionContacts),
 * na `saved`, czyli danych faktycznie zapisanych przez backend, nie na samym
 * szkicu. Stąd wymóg, że ten krok najpierw próbuje zapisać (page.tsx).
 */
export function StepSummary({
  saved,
  saving,
  structuralGaps,
  saveError,
  onNavigate,
  onRetrySave,
  onPublish,
  publishing,
  publishError,
  published,
}: {
  saved: OperatorCompetition | null;
  saving: boolean;
  structuralGaps: readonly string[];
  saveError: string | null;
  onNavigate: (step: WizardStepId) => void;
  onRetrySave: () => void;
  onPublish: () => void;
  publishing: boolean;
  publishError: string | null;
  published: boolean;
}) {
  const [confirming, setConfirming] = useState(false);

  if (published && saved !== null) {
    return (
      <div className="flex flex-col gap-3 rounded-sm border border-border-muted bg-surface-muted px-4 py-4">
        <p className="text-lg font-semibold">
          Konkurs {saved.number} został opublikowany.
        </p>
        <p className="text-sm">
          Jest teraz widoczny publicznie i, od rozpoczęcia naboru, przyjmuje
          wnioski.
        </p>
        <a
          className="self-start text-text-link underline"
          href={`/competitions/${saved.id}`}
          target="_blank"
          rel="noopener noreferrer"
        >
          Zobacz publiczną stronę konkursu
        </a>
      </div>
    );
  }

  if (structuralGaps.length > 0) {
    return (
      <div className="flex flex-col gap-2 text-sm">
        <p>
          Żeby zobaczyć podgląd, uzupełnij najpierw: {structuralGaps.join(", ")}.
        </p>
      </div>
    );
  }

  if (saving && saved === null) {
    return <p className="text-sm">Przygotowywanie podglądu…</p>;
  }

  if (saved === null) {
    return (
      <div className="flex flex-col gap-2 text-sm">
        <p role="alert">
          {saveError ?? "Nie udało się przygotować podglądu."}
        </p>
        <button
          type="button"
          className="self-start underline"
          onClick={onRetrySave}
        >
          Spróbuj ponownie
        </button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <p className="text-sm">
        Dokładnie to zobaczy gość po opublikowaniu, od strony numeru
        konkursu po osoby kontaktowe.
      </p>

      <PreviewSection title="Dane konkursu" step="basics" onNavigate={onNavigate}>
        <p className="text-sm">
          Nr {saved.number} · {statusLabels[saved.status]}
        </p>
        <p className="text-xl">{saved.title}</p>
      </PreviewSection>

      {saved.description === null ? null : (
        <PreviewSection
          title="Opis konkursu"
          step="description"
          onNavigate={onNavigate}
        >
          <Paragraphs text={saved.description} />
        </PreviewSection>
      )}

      <PreviewSection title="Terminy i kwoty" step="limits" onNavigate={onNavigate}>
        <CompetitionFacts competition={saved} />
      </PreviewSection>

      <PreviewSection
        title="Wymagane załączniki"
        step="attachments"
        onNavigate={onNavigate}
      >
        <CompetitionAttachments attachments={saved.attachments} />
      </PreviewSection>

      <PreviewSection
        title="Kontakt w sprawie konkursu"
        step="contacts"
        onNavigate={onNavigate}
      >
        <CompetitionContacts contacts={saved.contacts} />
      </PreviewSection>

      <div className="flex flex-col gap-2 rounded-sm border border-border-muted px-3 py-3">
        {confirming ? (
          <div className="flex flex-col gap-2 text-sm">
            <p>
              Opublikowany konkurs jest widoczny publicznie i, od terminu
              rozpoczęcia naboru, zaczyna przyjmować wnioski. Cofnięcie tego
              jest kosztowne wizerunkowo.
            </p>
            <div className="flex gap-3">
              <button
                type="button"
                className="rounded-sm border border-brand-accent px-4 py-2 text-brand-accent-text hover:bg-brand-accent hover:text-bg disabled:opacity-40"
                disabled={publishing}
                onClick={onPublish}
              >
                {publishing ? "Publikowanie…" : "Tak, opublikuj"}
              </button>
              <button
                type="button"
                className="underline"
                disabled={publishing}
                onClick={() => setConfirming(false)}
              >
                Anuluj
              </button>
            </div>
          </div>
        ) : (
          <button
            type="button"
            className="self-start rounded-sm border border-brand-accent px-4 py-2 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg"
            onClick={() => setConfirming(true)}
          >
            Opublikuj konkurs
          </button>
        )}

        {publishError !== null ? (
          <p role="alert" className="text-sm text-brand-accent-text">
            {publishError}
          </p>
        ) : null}
      </div>
    </div>
  );
}

function PreviewSection({
  title,
  step,
  onNavigate,
  children,
}: {
  title: string;
  step: WizardStepId;
  onNavigate: (step: WizardStepId) => void;
  children: React.ReactNode;
}) {
  return (
    <section className="flex flex-col gap-2">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-lg">{title}</h2>
        <button
          type="button"
          className="text-sm text-text-link underline"
          onClick={() => onNavigate(step)}
        >
          Popraw
        </button>
      </div>
      {children}
    </section>
  );
}

/** Same rendering as the public competition page (app/competitions/[id]/page.tsx). */
function Paragraphs({ text }: { text: string }) {
  const paragraphs = text
    .split(/\n{2,}/)
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph.length > 0);

  return (
    <div className="flex max-w-prose flex-col gap-3">
      {paragraphs.map((paragraph, index) => (
        <p key={index} className="whitespace-pre-line">
          {paragraph}
        </p>
      ))}
    </div>
  );
}
