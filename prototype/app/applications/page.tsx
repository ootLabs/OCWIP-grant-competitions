import { AppBar } from "@/components/AppBar";
import { ScreenFrame } from "@/components/ScreenFrame";
import { StatusChip } from "@/components/StatusChip";
import { Stepper } from "@/components/Stepper";

const WIZARD_LABELS = ["", "", "", "", "", ""];

const TIMELINE = [
  { label: "Utworzona", value: "21.09, 20:31" },
  { label: "Ostatni zapis roboczy", value: "29.09, 10:47" },
  { label: "Złożona", value: "29.09, 11:02" },
  { label: "Wynik", value: "do 31.10" },
];

export default function ApplicationsPage() {
  return (
    <ScreenFrame className="max-w-5xl">
      <AppBar context="Moje wnioski" account="Grupa nieformalna Łąka" />

      <div className="flex flex-col gap-6 px-5 py-7 sm:px-7">
        <div className="flex flex-col gap-2">
          <h1 className="text-3xl leading-snug">Wasze wnioski</h1>
          <p className="text-base leading-relaxed text-text-link">
            Widzicie tu wyłącznie swoje oferty. W jednym konkursie możecie złożyć więcej
            niż jedną i nic tego nie blokuje.
          </p>
        </div>

        <div className="flex flex-col gap-3">
          <article className="flex flex-col gap-3.5 rounded-[var(--radius-sm)] border border-border p-5">
            <div className="flex flex-wrap items-start gap-4">
              <span className="flex grow flex-col gap-1.5">
                <span className="text-lg font-semibold">Zielone podwórko na Sosnowej</span>
                <span className="text-[13px] leading-relaxed text-text-link">
                  Mikrodotacje na inicjatywy sąsiedzkie 2026 &nbsp;·&nbsp; nr MIS/2026/088
                  &nbsp;·&nbsp; 8 390 zł
                </span>
              </span>
              <StatusChip variant="accent">Złożona</StatusChip>
            </div>

            <div className="flex flex-wrap gap-4 rounded-[var(--radius-sm)] bg-surface-muted px-4 py-3.5">
              {TIMELINE.map((item) => (
                <div key={item.label} className="flex grow basis-32 flex-col gap-1">
                  <span className="text-xs text-text-link">{item.label}</span>
                  <span className="text-sm font-semibold">{item.value}</span>
                </div>
              ))}
            </div>

            <div className="flex flex-wrap items-center gap-3">
              <span className="inline-flex min-h-[44px] items-center rounded-[var(--radius-sm)] border border-border px-5 text-sm font-semibold text-text-link">
                Zobacz ofertę
              </span>
              <span className="inline-flex min-h-[44px] items-center rounded-[var(--radius-sm)] border border-border px-5 text-sm font-semibold text-text-link">
                Pobierz PDF
              </span>
              <span className="grow" />
              <span className="text-[13px] text-text-link">
                Złożonej oferty nie da się już edytować.
              </span>
            </div>
          </article>

          <article className="flex flex-col gap-3.5 rounded-[var(--radius-sm)] border-2 border-brand-accent p-5">
            <div className="flex flex-wrap items-start gap-4">
              <span className="flex grow flex-col gap-1.5">
                <span className="text-lg font-semibold">Sąsiedzka wymiana książek</span>
                <span className="text-[13px] leading-relaxed text-text-link">
                  Mikrodotacje na inicjatywy sąsiedzkie 2026 &nbsp;·&nbsp; ten sam konkurs,
                  druga oferta
                </span>
              </span>
              <StatusChip>Wersja robocza</StatusChip>
            </div>

            <div className="flex flex-col gap-2">
              <div className="flex flex-wrap items-baseline gap-3">
                <span className="text-[13px] font-semibold">Wypełnione 2 kroki z 6</span>
                <span className="grow" />
                <span className="text-[13px] text-text-link">
                  Ostatni zapis: 26.09, 22:15
                </span>
              </div>
              <Stepper labels={WIZARD_LABELS} current={1} showLabels={false} />
            </div>

            <div className="flex flex-wrap items-center gap-3">
              <span className="inline-flex min-h-[44px] items-center rounded-[var(--radius-sm)] bg-brand-accent px-5 text-sm font-semibold text-bg">
                Wróć do wypełniania
              </span>
              <span className="grow" />
              <span className="text-[13px] font-semibold text-brand-accent-text">
                Zostały 2 dni do zamknięcia naboru
              </span>
            </div>
          </article>

          <article className="flex flex-col gap-3.5 rounded-[var(--radius-sm)] border border-border p-5">
            <div className="flex flex-wrap items-start gap-4">
              <span className="flex grow flex-col gap-1.5">
                <span className="text-lg font-semibold">
                  Warsztaty naprawcze w świetlicy
                </span>
                <span className="text-[13px] leading-relaxed text-text-link">
                  Mikrodotacje na inicjatywy sąsiedzkie 2025 &nbsp;·&nbsp; nr MIS/2025/041
                  &nbsp;·&nbsp; 7 200 zł
                </span>
              </span>
              <StatusChip>Bez dofinansowania</StatusChip>
            </div>
            <span className="text-[13px] leading-relaxed text-text-link">
              Oferta z poprzedniej edycji. Zostaje na koncie razem z całą historią
              statusów, bo dokumentację konkursów trzymamy minimum pięć lat.
            </span>
          </article>
        </div>
      </div>
    </ScreenFrame>
  );
}
