import { AppBar } from "@/components/AppBar";
import { ScreenFrame } from "@/components/ScreenFrame";

const ENTITY_TYPES = [
  {
    title: "Grupa nieformalna",
    note: "Trzy osoby fizyczne działające razem. Nie potrzebujecie NIP-u, KRS-u ani konta organizacji.",
    selected: true,
  },
  {
    title: "Grupa nieformalna pod patronatem organizacji",
    note: "Działacie samodzielnie, ale organizacja użycza wam osobowości prawnej. Poprosimy o jej dane.",
    selected: false,
  },
  {
    title: "Organizacja",
    note: "Stowarzyszenie, fundacja lub inny podmiot z NIP-em i adresem siedziby.",
    selected: false,
  },
];

/** Read-only stand-in for an input, so the mockup never looks half wired up. */
function FieldValue({ children }: { children: React.ReactNode }) {
  return (
    <span className="block rounded-[var(--radius-sm)] border border-border px-3.5 py-3 text-base">
      {children}
    </span>
  );
}

export default function RegistrationPage() {
  return (
    <ScreenFrame>
      <AppBar context="Zakładanie konta" />

      <div className="flex flex-col gap-6 px-5 py-8 sm:px-7">
        <div className="flex flex-col gap-2.5">
          <h1 className="text-3xl leading-tight">Kim jesteście?</h1>
          <p className="text-base leading-relaxed text-text-link">
            Od tego zależy tylko to, o co zapytamy dalej. Żadna z tych trzech odpowiedzi
            nie jest gorsza od pozostałych i żadna nie zmniejsza szans w konkursie.
          </p>
        </div>

        <div className="flex flex-col gap-2.5">
          {ENTITY_TYPES.map((type) => (
            <div
              key={type.title}
              className={`flex items-start gap-3.5 rounded-[var(--radius-sm)] p-4.5 ${
                type.selected ? "border-2 border-brand-accent" : "border border-border"
              }`}
            >
              <span
                className={`mt-0.5 h-5 w-5 shrink-0 rounded-[var(--radius-pill)] ${
                  type.selected
                    ? "border-[6px] border-brand-accent"
                    : "border border-border"
                }`}
                aria-hidden
              />
              <span className="flex flex-col gap-1">
                <span className="text-base font-semibold">{type.title}</span>
                <span className="text-[13px] leading-relaxed text-text-link">
                  {type.note}
                </span>
              </span>
            </div>
          ))}
        </div>

        <div className="flex flex-col gap-4.5 rounded-[var(--radius-sm)] bg-surface-muted p-5">
          <span className="text-base font-semibold">
            Dane, o które pytamy przy tej odpowiedzi
          </span>

          <div className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold">Nazwa grupy</span>
            <FieldValue>Grupa nieformalna Łąka</FieldValue>
          </div>

          <div className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold">Trzy osoby reprezentujące</span>
            <FieldValue>Anna Wieczorek</FieldValue>
            <FieldValue>Marek Sobota</FieldValue>
            <FieldValue>Julia Pawlak</FieldValue>
          </div>

          <div className="flex flex-col gap-1.5">
            {/* Address is personal data: flagged here so encryption work does not miss it. */}
            <span className="text-sm font-semibold">Adres korespondencyjny</span>
            <FieldValue>ul. Sosnowa 14/3, 45-062 Opole</FieldValue>
            <span className="text-[13px] leading-relaxed text-text-link">
              Może być adres prywatny. Grupa nieformalna nie ma siedziby i nikt tego od
              was nie oczekuje.
            </span>
          </div>
        </div>

        <div className="flex flex-col gap-4.5">
          <div className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold">Adres e-mail</span>
            <FieldValue>lakasosnowa@example.org</FieldValue>
            <span className="text-[13px] leading-relaxed text-text-link">
              To jedyny kanał, którym poinformujemy was o wyniku konkursu.
            </span>
          </div>
          <div className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold">Hasło</span>
            <FieldValue>••••••••••••</FieldValue>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-3.5">
          <span className="inline-flex min-h-[52px] items-center justify-center rounded-[var(--radius-sm)] bg-brand-accent px-7 text-base font-semibold text-bg">
            Załóż konto
          </span>
          <span className="text-sm text-text-link">Macie już konto? Zaloguj się</span>
        </div>

        <div className="flex flex-col gap-2 rounded-[var(--radius-sm)] border border-border p-5">
          <span className="text-sm font-semibold">
            Co zobaczycie po kliknięciu, niezależnie od wyniku
          </span>
          <span className="text-[13px] leading-relaxed text-text-link">
            „Jeśli ten adres nie był jeszcze użyty, wysłaliśmy na niego link aktywacyjny."
            Ten sam komunikat pojawia się dla adresu wolnego i zajętego, bo system nie może
            zdradzać, kto ma tu konto.
          </span>
        </div>
      </div>
    </ScreenFrame>
  );
}
