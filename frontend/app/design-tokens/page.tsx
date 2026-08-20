import Link from "next/link";
import { contrastRatio, meetsAA, WCAG_AA_LARGE_TEXT, WCAG_AA_TEXT } from "@/lib/contrast";
import { ContrastToggle } from "./contrast-toggle";

// Shown to the client to sign off on colours and typography before any real
// screen exists (card T-15.1). Hex values below are read-only labels, never
// used to style anything: every swatch, button and text sample is painted
// through the token classes defined in app/globals.css.
const colorPairs: Array<{
  role: string;
  swatchClassName: string;
  fg: string;
  bg: string;
  largeText?: boolean;
  note?: string;
}> = [
  {
    role: "Tekst podstawowy na tle strony",
    swatchClassName: "bg-bg text-text border border-border",
    fg: "#231F20",
    bg: "#FFFFFF",
  },
  {
    role: "Link w spoczynku na tle strony",
    swatchClassName: "bg-bg text-text-link border border-border",
    fg: "#413D39",
    bg: "#FFFFFF",
  },
  {
    role: "Akcent marki jako mały tekst / link (hover, aktywne)",
    swatchClassName: "bg-bg text-brand-accent-hover border border-border",
    fg: "#9F3A0C",
    bg: "#FFFFFF",
  },
  {
    role: "Akcent marki jako duże UI (przyciski, obramowania)",
    swatchClassName: "bg-brand-accent text-bg",
    fg: "#FFFFFF",
    bg: "#CF4B0F",
    largeText: true,
    note: "Na granicy AA dla zwykłego tekstu (patrz niżej), mały tekst ma używać ciemniejszego wariantu wyżej.",
  },
  {
    role: "Pomarańcz z logo (wyłącznie grafika/logo)",
    swatchClassName: "bg-brand-logo-orange text-bg",
    fg: "#FFFFFF",
    bg: "#EB6209",
    note: "Nie przechodzi AA dla zwykłego tekstu. Używać tylko w logo/grafice, nigdy jako kolor tekstu czy linku.",
  },
];

const contrastModePairs: Array<{ role: string; fg: string; bg: string }> = [
  { role: "Tekst na tle (tryb kontrastu)", fg: "#FFFFFF", bg: "#000000" },
  { role: "Aktywny stan, żółty na czarnym", fg: "#FFE800", bg: "#000000" },
  { role: "Akcent marki na czarnym", fg: "#CF4B0F", bg: "#000000" },
];

function ContrastBadge({ fg, bg, largeText }: { fg: string; bg: string; largeText?: boolean }) {
  const ratio = contrastRatio(fg, bg);
  const pass = meetsAA(fg, bg, largeText);
  const threshold = largeText ? WCAG_AA_LARGE_TEXT : WCAG_AA_TEXT;
  return (
    <span
      className={
        pass
          ? "rounded-[var(--radius-sm)] bg-surface-muted px-2 py-1 text-xs font-semibold text-text"
          : "rounded-[var(--radius-sm)] bg-brand-logo-orange px-2 py-1 text-xs font-semibold text-bg"
      }
    >
      {ratio.toFixed(2)}:1 · próg {threshold}:1 · {pass ? "PASS" : "FAIL"}
    </span>
  );
}

export default function DesignTokensPage() {
  return (
    <main className="mx-auto flex max-w-3xl flex-col gap-10 px-6 py-16">
      <div>
        <Link href="/" className="text-sm underline">
          &larr; Wróć do strony startowej
        </Link>
        <h1 className="mt-4 text-3xl">Design tokeny OCWIP</h1>
        <p className="mt-2 text-text/70">
          Podgląd tokenów brandingowych z karty T-07, żeby dało się je pokazać
          klientce przed powstaniem gotowych ekranów. Wartości kontrastu
          poniżej są liczone na żywo, narzędziem (
          <code>lib/contrast.ts</code>), nie przepisane ręcznie.
        </p>
      </div>

      <section className="flex flex-col gap-3">
        <h2 className="text-xl">Logo</h2>
        <div className="flex items-center gap-4 rounded-lg border border-border bg-surface-muted p-6">
          {/* eslint-disable-next-line @next/next/no-img-element -- vector logo, no optimisation needed */}
          <img src="/ocwip-logo.svg" alt="Logo OCWIP" className="h-14 w-auto" />
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-xl">Kolory i kontrast (WCAG 2.1 AA)</h2>
        <ul className="flex flex-col gap-3">
          {colorPairs.map((pair) => (
            <li
              key={pair.role}
              className="flex flex-col gap-2 rounded-lg border border-border p-4 sm:flex-row sm:items-center sm:justify-between"
            >
              <div className="flex items-center gap-3">
                <span
                  className={`flex h-10 w-10 items-center justify-center rounded-[var(--radius-sm)] text-xs ${pair.swatchClassName}`}
                  aria-hidden
                >
                  Aa
                </span>
                <div>
                  <p className="font-medium">{pair.role}</p>
                  <p className="text-sm text-text/70">
                    {pair.fg} na {pair.bg}
                  </p>
                  {pair.note ? <p className="text-sm text-text/70">{pair.note}</p> : null}
                </div>
              </div>
              <ContrastBadge fg={pair.fg} bg={pair.bg} largeText={pair.largeText} />
            </li>
          ))}
        </ul>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-xl">Tryb wysokiego kontrastu</h2>
        <p className="text-text/70">
          Osobny, kompletny zestaw tokenów (<code>[data-contrast=&quot;true&quot;]</code>),
          nie doklejane nadpisania na końcu. Przełącz, żeby zobaczyć podgląd.
        </p>
        <ul className="flex flex-col gap-2 text-sm text-text/70">
          {contrastModePairs.map((pair) => (
            <li key={pair.role} className="flex items-center justify-between gap-3">
              <span>{pair.role}</span>
              <ContrastBadge fg={pair.fg} bg={pair.bg} />
            </li>
          ))}
        </ul>
        <ContrastToggle />
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-xl">Typografia</h2>
        <div className="rounded-lg border border-border p-4">
          <h1 className="text-3xl">Nagłówek h1 (Playfair Display 800)</h1>
          <h2 className="mt-2 text-2xl">Nagłówek h2</h2>
          <h3 className="mt-2 text-xl">Nagłówek h3</h3>
          <p className="mt-4 font-body">
            Treść w Poppins, waga 400. Świadomy kontrast: nagłówki szeryfowe,
            treść bezszeryfowa, buduje &quot;edytorski&quot;, nie korporacyjny
            charakter marki.
          </p>
          <p className="mt-2 font-body font-semibold">Poppins, waga 600 (np. etykiety, przyciski).</p>
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-xl">Odstępy</h2>
        <div className="flex flex-col gap-2">
          {(
            [
              ["--space-1", "0.25rem"],
              ["--space-2", "0.5rem"],
              ["--space-3", "1rem"],
              ["--space-4", "1.5rem"],
              ["--space-5", "3rem"],
            ] as const
          ).map(([name, value]) => (
            <div key={name} className="flex items-center gap-3">
              <span className="w-28 text-sm text-text/70">
                {name} ({value})
              </span>
              <span
                className="h-4 bg-brand-accent"
                style={{ width: `var(${name})` }}
                aria-hidden
              />
            </div>
          ))}
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-xl">Promienie i przyciski</h2>
        <div className="flex flex-wrap items-center gap-4">
          <button
            type="button"
            className="rounded-[var(--radius-sm)] bg-brand-accent px-4 py-2 font-semibold text-bg hover:bg-brand-accent-hover"
          >
            Przycisk podstawowy
          </button>
          <button
            type="button"
            className="rounded-[var(--radius-sm)] border-2 border-brand-accent px-4 py-2 font-semibold text-brand-accent hover:bg-brand-accent hover:text-bg"
          >
            Przycisk obrysowany
          </button>
          <span className="rounded-[var(--radius-pill)] border border-border px-4 py-2 text-sm text-text/70">
            Pole w kształcie pigułki (--radius-pill)
          </span>
        </div>
      </section>
    </main>
  );
}
