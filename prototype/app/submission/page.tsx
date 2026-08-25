import { ScreenFrame } from "@/components/ScreenFrame";

const SUMMARY_ROWS = [
  { label: "Dane podmiotu", value: "Grupa nieformalna Łąka" },
  { label: "Tytuł, miejsce i termin", value: "Zielone podwórko na Sosnowej" },
  { label: "Opis zadania", value: "198 z 2 000 znaków" },
  { label: "Odbiorcy", value: "około 40 osób" },
  { label: "Budżet", value: "8 390 zł z 9 000 zł" },
  { label: "Załączniki", value: "2 wymagane, 2 dodane" },
];

export default function SubmissionPage() {
  return (
    <ScreenFrame>
      <div className="flex flex-col gap-6 px-5 py-8 sm:px-7">
        <div className="flex flex-col gap-2.5">
          <span className="text-[13px] font-semibold">Ostatni krok</span>
          <h1 className="text-3xl leading-tight">Sprawdźcie i złóżcie ofertę</h1>
          <p className="text-base leading-relaxed text-text-link">
            Po złożeniu nie da się już nic zmienić ani cofnąć. Do tego momentu możecie
            wracać do wniosku dowolnie wiele razy.
          </p>
        </div>

        <div className="flex flex-col overflow-hidden rounded-[var(--radius-sm)] border border-border">
          {SUMMARY_ROWS.map((row, index) => (
            <div
              key={row.label}
              className={`flex flex-wrap items-center gap-3.5 px-4.5 py-3.5 ${
                index === SUMMARY_ROWS.length - 1 ? "" : "border-b border-border-muted"
              }`}
            >
              <span
                className="h-5 w-5 shrink-0 rounded-[var(--radius-pill)] border-2 border-brand-accent bg-brand-accent"
                aria-hidden
              />
              <span className="grow text-[15px]">{row.label}</span>
              <span className="text-[13px] text-text-link">{row.value}</span>
              <span className="text-[13px] font-semibold text-brand-accent-text">
                Zmień
              </span>
            </div>
          ))}
        </div>

        <div className="flex flex-col gap-2.5 rounded-[var(--radius-sm)] border-2 border-brand-accent px-5 py-5">
          <span className="text-[17px] font-semibold">Zostało 2 dni i 4 godziny</span>
          <span className="text-[15px] leading-relaxed text-text-link">
            Nabór zamyka się 30 września o godz. 12:00. O 12:01 przycisk poniżej przestanie
            działać i nie będzie od tego odwołania ani wyjątku. Nie zostawiajcie złożenia
            na ostatnią godzinę.
          </span>
        </div>

        <div className="flex items-start gap-3.5 rounded-[var(--radius-sm)] bg-surface-muted px-5 py-4.5">
          <span
            className="mt-0.5 h-5 w-5 shrink-0 rounded-[3px] border-2 border-brand-accent"
            aria-hidden
          />
          <span className="text-[15px] leading-relaxed">
            Oświadczamy, że dane w ofercie są prawdziwe, a osoby wymienione we wniosku
            wiedzą o jego złożeniu i wyraziły na to zgodę.
          </span>
        </div>

        <div className="flex flex-wrap items-center gap-3.5">
          <span className="inline-flex min-h-[52px] items-center justify-center rounded-[var(--radius-sm)] bg-brand-accent px-7 text-base font-semibold text-bg">
            Złóż ofertę, tego nie da się cofnąć
          </span>
          <span className="inline-flex min-h-[52px] items-center justify-center rounded-[var(--radius-sm)] border border-border px-6 text-base font-semibold text-text-link">
            Zapisz i wróć później
          </span>
        </div>

        <span className="text-[13px] leading-relaxed text-text-link">
          Po złożeniu dostaniecie numer oferty i potwierdzenie na adres
          lakasosnowa@example.org. Wniosek w formie PDF będzie do pobrania z waszego konta.
        </span>
      </div>
    </ScreenFrame>
  );
}
