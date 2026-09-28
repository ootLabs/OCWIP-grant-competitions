"use client";

import Link from "next/link";

import {
  CompetitionAttachments,
  CompetitionContacts,
} from "@/app/competitions/competition-attachments";
import { CompetitionFacts } from "@/app/competitions/competition-facts";
import { statusLabels } from "@/app/competitions/labels";
import type { WizardStepId } from "@/lib/competition-wizard/types";
import type { OperatorCompetition } from "@/lib/operator-competitions";

/**
 * Krok 1.7: Podsumowanie (docs/runbook/pola.md). Publikacja przeszła na
 * stronę konkursu (T-97): wymaga opublikowanego formularza i obu kart oceny,
 * a te da się ułożyć dopiero dla zapisanego konkursu.
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
}: {
  saved: OperatorCompetition | null;
  saving: boolean;
  structuralGaps: readonly string[];
  saveError: string | null;
  onNavigate: (step: WizardStepId) => void;
  onRetrySave: () => void;
}) {
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

      <div className="flex flex-col gap-2 rounded-sm border border-border-muted px-3 py-3 text-sm">
        <p>
          Szkic jest zapisany. Konkurs publikujesz na jego stronie, kiedy ma już
          formularz wniosku i obie karty oceny.
        </p>
        <Link
          className="self-start text-text-link underline"
          href={`/panel/operator/competitions/${saved.id}`}
        >
          Przejdź do strony konkursu
        </Link>
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
