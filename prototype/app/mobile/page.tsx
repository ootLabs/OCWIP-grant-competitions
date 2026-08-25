import { AppBar } from "@/components/AppBar";
import { Stepper } from "@/components/Stepper";

const STEP_LABELS = ["", "", "", "", "", ""];

/**
 * Fixed 390 by 844: the point of this screen is the phone, so it does not
 * stretch. No painted status bar and no fake keyboard, because on a real device
 * the system draws its own on top and a painted copy looks doubled up.
 */
export default function MobilePage() {
  return (
    <div className="flex flex-col gap-4">
      <p className="max-w-xl text-sm leading-relaxed text-text-link">
        Ten sam krok kreatora na szerokości 390 pikseli. Wśród wnioskodawców są grupy
        nieformalne bez biura i firmowego sprzętu, dla części z nich telefon będzie
        jedynym urządzeniem.
      </p>

      <div className="flex h-[844px] w-[390px] flex-col overflow-hidden rounded-[var(--radius-sm)] border border-border bg-bg">
        <AppBar context="Kreator" account="Zapisano" compact />

        <div className="flex grow flex-col gap-4 p-4">
          <div className="flex flex-col gap-2">
            <div className="flex items-baseline gap-2">
              <span className="text-[13px] font-semibold">Krok 3 z 6</span>
              <span className="text-[13px] text-text-link">Opis</span>
            </div>
            <Stepper labels={STEP_LABELS} current={2} showLabels={false} />
          </div>

          <h1 className="text-[26px] leading-snug">Opiszcie, co chcecie zrobić</h1>

          <p className="text-[15px] leading-relaxed text-text-link">
            Napiszcie tak, jakbyście tłumaczyli sąsiadowi. Konkret waży więcej niż styl.
          </p>

          <span className="block min-h-[208px] rounded-[var(--radius-sm)] border border-border p-3.5 text-base leading-relaxed">
            Chcemy odnowić podwórko między blokami przy ul. Sosnowej. Postawimy dwie ławki,
            posadzimy żywopłot od strony parkingu i zorganizujemy dwa sobotnie spotkania
            sąsiedzkie.
          </span>

          <div className="flex items-baseline gap-2">
            <span className="text-xs text-text-link">Zapisujemy w tle</span>
            <span className="grow" />
            <span className="text-xs text-text-link">Pozostało 1 802 znaki</span>
          </div>

          <div className="flex flex-col gap-1.5 rounded-[var(--radius-sm)] bg-surface-muted px-4 py-3.5">
            <span className="text-[13px] font-semibold">Warto, żeby znalazło się tu</span>
            <span className="text-[13px] leading-relaxed text-text-link">
              co po kolei zrobicie, kto to zrobi, co zostanie po zakończeniu
            </span>
          </div>
        </div>

        <div className="flex flex-col gap-2.5 border-t border-border p-4">
          <span className="flex min-h-[52px] items-center justify-center rounded-[var(--radius-sm)] bg-brand-accent text-base font-semibold text-bg">
            Zapisz i przejdź dalej
          </span>
          <div className="flex items-center gap-3">
            <span className="flex min-h-[48px] grow items-center justify-center rounded-[var(--radius-sm)] border border-border text-[15px] font-semibold text-text-link">
              Wróć
            </span>
            <span className="flex min-h-[48px] grow items-center justify-center rounded-[var(--radius-sm)] border border-border text-[15px] font-semibold text-text-link">
              Zapisz i wyjdź
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
