import { ScreenFrame } from "@/components/ScreenFrame";
import { StatusChip } from "@/components/StatusChip";

export default function AttachmentsPage() {
  return (
    <ScreenFrame>
      <div className="flex flex-col gap-5 px-5 py-8 sm:px-7">
        <div className="flex flex-col gap-2.5">
          <span className="text-[13px] font-semibold">Krok 6 z 6</span>
          <h1 className="text-3xl leading-snug">Co dołączacie do oferty?</h1>
          <p className="text-base leading-relaxed text-text-link">
            Dwa załączniki są w tym konkursie wymagane. Pozostałe dodajcie, jeśli uważacie,
            że pomogą komisji zrozumieć zadanie.
          </p>
        </div>

        <div className="flex flex-col gap-2.5">
          <div className="flex flex-wrap items-center gap-4 rounded-[var(--radius-sm)] border border-border px-4.5 py-4">
            <span className="flex grow flex-col gap-1">
              <span className="text-base font-semibold">
                Oświadczenie o niekaralności
              </span>
              <span className="text-[13px] leading-relaxed text-text-link">
                wymagany &nbsp;·&nbsp; oswiadczenie-niekaralnosc.pdf &nbsp;·&nbsp; 240 kB
                &nbsp;·&nbsp; dodany 28 września, 19:12
              </span>
            </span>
            <StatusChip variant="accent">Dodany</StatusChip>
            <span className="text-[13px] text-text-link underline">Usuń</span>
          </div>

          <div className="flex flex-wrap items-center gap-4 rounded-[var(--radius-sm)] border-2 border-brand-accent px-4.5 py-4">
            <span className="flex grow flex-col gap-1">
              <span className="text-base font-semibold">Zgoda właściciela terenu</span>
              <span className="text-[13px] leading-relaxed text-text-link">
                wymagany, jeszcze go nie ma. Bez tego pliku nie da się złożyć oferty.
              </span>
            </span>
            <span className="inline-flex min-h-[48px] items-center rounded-[var(--radius-sm)] bg-brand-accent px-5 text-[15px] font-semibold text-bg">
              Dodaj plik
            </span>
          </div>

          <div className="flex flex-wrap items-center gap-4 rounded-[var(--radius-sm)] border border-border px-4.5 py-4">
            <span className="flex grow flex-col gap-1">
              <span className="text-base font-semibold">Zdjęcia podwórka</span>
              <span className="text-[13px] leading-relaxed text-text-link">
                nieobowiązkowy &nbsp;·&nbsp; 2 pliki &nbsp;·&nbsp; 1,8 MB &nbsp;·&nbsp;
                dodane 27 września, 21:40
              </span>
            </span>
            <StatusChip>Dodane</StatusChip>
            <span className="text-[13px] text-text-link underline">Usuń</span>
          </div>
        </div>

        <div className="flex flex-col items-center justify-center gap-2 rounded-[var(--radius-sm)] border-2 border-dashed border-border p-8">
          <span className="text-base font-semibold">Przeciągnijcie pliki tutaj</span>
          <span className="text-[13px] text-text-link">
            albo kliknijcie, żeby wybrać z dysku
          </span>
          <span className="text-center text-xs font-semibold text-brand-accent-text">
            Dopuszczalne formaty i maksymalny rozmiar: [DO USTALENIA Z OCWIP]
          </span>
        </div>

        <div className="flex flex-col gap-2 rounded-[var(--radius-sm)] bg-surface-muted px-5 py-4.5">
          <span className="text-sm font-semibold">Załączniki zostają z wnioskiem</span>
          <span className="text-[13px] leading-relaxed text-text-link">
            Pliki trafiają do repozytorium oferty i zostają tam razem z całą historią zmian
            statusu. Nic nie kasujemy: „usunięcie" oznacza oznaczenie pliku jako
            nieaktywnego, bo dokumentację konkursu trzymamy minimum pięć lat.
          </span>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <span className="inline-flex min-h-[48px] items-center justify-center rounded-[var(--radius-sm)] border border-border bg-surface-muted px-6 text-[15px] font-semibold text-text-link">
            Dodaj brakujący załącznik, żeby przejść dalej
          </span>
          <span className="inline-flex min-h-[48px] items-center justify-center rounded-[var(--radius-sm)] border border-border px-5 text-[15px] font-semibold text-text-link">
            Wróć do budżetu
          </span>
        </div>
      </div>
    </ScreenFrame>
  );
}
