import Link from "next/link";
import { SCREENS } from "@/lib/screens";

export default function OverviewPage() {
  return (
    <div className="flex max-w-4xl flex-col gap-8">
      <div className="flex flex-col gap-3">
        <h1 className="text-4xl leading-tight">Ścieżka wnioskodawcy</h1>
        <p className="text-[17px] leading-relaxed text-text-link">
          Dziewięć ekranów kierunku C, od wejścia na stronę konkursu do potwierdzenia
          złożenia oferty. Jedno pytanie naraz, treść 16 do 17 pikseli, cele dotyku od 48
          pikseli, autozapis i postęp widoczne bez klikania, pomarańcz niosący następny
          ruch.
        </p>
      </div>

      <div className="flex flex-col gap-3">
        {SCREENS.map((screen) => (
          <Link
            key={screen.slug}
            href={`/${screen.slug}`}
            className="flex items-start gap-4 rounded-[var(--radius-sm)] border border-border p-5 no-underline hover:border-brand-accent"
          >
            <span className="w-6 shrink-0 pt-0.5 text-sm font-semibold text-brand-accent-text">
              {screen.step}
            </span>
            <span className="flex flex-col gap-1.5">
              <span className="flex flex-wrap items-center gap-3">
                <span className="text-lg font-semibold text-text">{screen.title}</span>
                {screen.interactive && (
                  <span className="text-xs font-semibold text-brand-accent-text">
                    klikalny
                  </span>
                )}
              </span>
              <span className="text-sm leading-relaxed text-text-link">
                {screen.summary}
              </span>
            </span>
          </Link>
        ))}
      </div>

      <div className="flex flex-col gap-2 rounded-[var(--radius-sm)] bg-surface-muted p-5">
        <span className="text-sm font-semibold">Czego tu celowo nie ma</span>
        <span className="text-sm leading-relaxed text-text-link">
          Oceny, umowy i sprawozdania nie projektujemy, bo nie mamy wzoru karty oceny,
          wzoru umowy ani wzoru sprawozdania. Brakuje też widoków operatora i recenzenta:
          kierunek C powstał dla wnioskodawcy, a dla operatora ten sam rytm byłby męczący.
        </span>
      </div>
    </div>
  );
}
