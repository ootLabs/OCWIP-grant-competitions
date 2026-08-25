import { AppBar } from "@/components/AppBar";
import { ScreenFrame } from "@/components/ScreenFrame";
import { StatusChip } from "@/components/StatusChip";

const ELIGIBLE = [
  "Organizacja pozarządowa z siedzibą w województwie opolskim",
  "Grupa nieformalna, czyli trzy osoby fizyczne działające razem",
  "Grupa nieformalna pod patronatem organizacji",
];

const STEPS_AHEAD = [
  "Dane podmiotu, w większości pobrane z waszego konta",
  "Tytuł, miejsce i termin zadania",
  "Opis tego, co chcecie zrobić",
  "Odbiorcy zadania",
  "Budżet, z limitem pilnowanym na bieżąco",
  "Dwa wymagane załączniki",
];

const FACTS = [
  { label: "Do zdobycia", value: "do 9 000 zł", note: "na jedno zadanie" },
  { label: "Nabór kończy się", value: "30 września", note: "o godz. 12:00, co do minuty" },
  { label: "Wyniki", value: "do 31 października", note: "mailem, do wszystkich" },
];

export default function CompetitionPage() {
  return (
    <ScreenFrame>
      <AppBar context="Konkursy" account="Zaloguj się" />

      <div className="flex flex-col gap-7 px-5 py-8 sm:px-7">
        <div className="flex flex-col gap-3">
          <span className="text-[13px] text-text-link">Wszystkie konkursy OCWIP</span>
          <div className="flex flex-wrap items-center gap-2.5">
            <StatusChip variant="accent">Nabór otwarty</StatusChip>
            <span className="text-[13px] font-semibold text-brand-accent-text">
              Zostały 2 dni
            </span>
          </div>
          <h1 className="text-4xl leading-tight">
            Mikrodotacje na inicjatywy sąsiedzkie 2026
          </h1>
          <p className="text-[17px] leading-relaxed text-text-link">
            Wspieramy małe działania robione przez mieszkańców dla mieszkańców: podwórka,
            świetlice, sąsiedzkie warsztaty, wydarzenia na osiedlu. Nie musicie mieć
            organizacji ani doświadczenia w pisaniu wniosków. Wystarczy pomysł i trzy
            osoby gotowe go zrealizować.
          </p>
        </div>

        <div className="flex flex-col gap-3 sm:flex-row">
          {FACTS.map((fact) => (
            <div
              key={fact.label}
              className="flex grow basis-0 flex-col gap-1 rounded-[var(--radius-sm)] bg-surface-muted p-4"
            >
              <span className="text-xs text-text-link">{fact.label}</span>
              <span className="text-xl font-semibold">{fact.value}</span>
              <span className="text-xs text-text-link">{fact.note}</span>
            </div>
          ))}
        </div>

        <div className="flex flex-col gap-3">
          <span className="text-[11px] font-semibold uppercase tracking-[0.14em] text-brand-accent-text">
            Kto może złożyć ofertę
          </span>
          <div className="flex flex-col gap-2">
            {ELIGIBLE.map((who) => (
              <span key={who} className="text-base leading-relaxed">
                {who}
              </span>
            ))}
          </div>
          <span className="text-sm leading-relaxed text-text-link">
            Jeden podmiot może złożyć więcej niż jedną ofertę w tym konkursie.
          </span>
        </div>

        <div className="flex flex-col gap-3.5 rounded-[var(--radius-sm)] border border-border p-5">
          <span className="font-heading text-[22px] leading-snug">
            Co was czeka, zanim złożycie ofertę
          </span>
          <div className="flex flex-col gap-2.5">
            {STEPS_AHEAD.map((step, index) => (
              <div key={step} className="flex items-baseline gap-3">
                <span className="w-5 shrink-0 text-sm font-semibold text-brand-accent-text">
                  {index + 1}
                </span>
                <span className="text-[15px] leading-snug">{step}</span>
              </div>
            ))}
          </div>
          <span className="text-sm leading-relaxed text-text-link">
            Nie musicie zrobić tego za jednym razem. Zapisujemy każdą zmianę, a do wniosku
            wracacie, kiedy chcecie.
          </span>
        </div>

        <div className="flex flex-wrap items-center gap-3.5">
          <span className="inline-flex min-h-[52px] items-center justify-center rounded-[var(--radius-sm)] bg-brand-accent px-7 text-base font-semibold text-bg">
            Zacznij wypełniać wniosek
          </span>
          <span className="inline-flex min-h-[52px] items-center justify-center rounded-[var(--radius-sm)] border-2 border-brand-accent px-6 text-base font-semibold text-brand-accent-text">
            Pobierz regulamin
          </span>
        </div>

        <div className="border-t border-border pt-3.5">
          <span className="text-xs leading-relaxed text-text-link">
            Konkurs finansowany ze środków [ŹRÓDŁO FINANSOWANIA DO POTWIERDZENIA],
            logotypy dostarcza OCWIP.
          </span>
        </div>
      </div>
    </ScreenFrame>
  );
}
