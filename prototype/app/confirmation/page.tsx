import { ScreenFrame } from "@/components/ScreenFrame";

const WHAT_HAPPENS_NEXT = [
  {
    when: "Do 30 września, 12:00",
    what: "Trwa nabór. Wasza oferta czeka razem z pozostałymi, nikt jej jeszcze nie czyta.",
    current: true,
  },
  {
    when: "Październik",
    what: "Oferty trafiają do oceniających. Każda dostaje kartę oceny i punkty, z których powstaje lista rankingowa.",
    current: false,
  },
  {
    when: "Do 31 października",
    what: "Piszemy do wszystkich, także do tych, którzy dotacji nie dostaną. Wynik zobaczycie również na swoim koncie.",
    current: false,
  },
];

export default function ConfirmationPage() {
  return (
    <ScreenFrame>
      <div className="flex flex-col gap-6 px-5 py-10 sm:px-7">
        <div className="flex items-start gap-4.5">
          <svg
            width="44"
            height="44"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="shrink-0 text-brand-accent"
            aria-hidden
          >
            <circle cx="12" cy="12" r="10" />
            <path d="M7.5 12.5l3 3 6-6.5" />
          </svg>
          <div className="flex flex-col gap-2.5">
            <h1 className="text-[34px] leading-tight">Oferta złożona</h1>
            <p className="text-[17px] leading-relaxed text-text-link">
              Macie to z głowy. Nic więcej nie musicie teraz robić, a o wyniku napiszemy do
              was mailem.
            </p>
          </div>
        </div>

        <div className="flex flex-col gap-3 sm:flex-row">
          <div className="flex grow basis-0 flex-col gap-1 rounded-[var(--radius-sm)] bg-surface-muted p-4.5">
            <span className="text-xs text-text-link">Numer oferty</span>
            <span className="text-[22px] font-semibold">MIS/2026/088</span>
          </div>
          <div className="flex grow basis-0 flex-col gap-1 rounded-[var(--radius-sm)] bg-surface-muted p-4.5">
            <span className="text-xs text-text-link">Data i godzina złożenia</span>
            <span className="text-[22px] font-semibold">29.09, 11:02</span>
            <span className="text-[13px] text-text-link">
              58 minut przed zamknięciem naboru
            </span>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-3.5">
          <span className="inline-flex min-h-[48px] items-center justify-center rounded-[var(--radius-sm)] bg-brand-accent px-6 text-[15px] font-semibold text-bg">
            Pobierz ofertę w PDF
          </span>
          <span className="inline-flex min-h-[48px] items-center justify-center rounded-[var(--radius-sm)] border border-border px-5 text-[15px] font-semibold text-text-link">
            Przejdź do moich wniosków
          </span>
        </div>

        <div className="flex flex-col gap-4 rounded-[var(--radius-sm)] border border-border p-6">
          <span className="font-heading text-[22px] leading-snug">
            Co się teraz stanie
          </span>
          {WHAT_HAPPENS_NEXT.map((stage) => (
            <div key={stage.when} className="flex items-start gap-3.5">
              <span
                className={`mt-1.5 h-2.5 w-2.5 shrink-0 rounded-[var(--radius-pill)] ${
                  stage.current ? "bg-brand-accent" : "bg-border"
                }`}
                aria-hidden
              />
              <span className="flex flex-col gap-1">
                <span className="text-[15px] font-semibold">{stage.when}</span>
                <span className="text-[13px] leading-relaxed text-text-link">
                  {stage.what}
                </span>
              </span>
            </div>
          ))}
        </div>

        <div className="flex flex-col gap-2 rounded-[var(--radius-sm)] bg-surface-muted px-5 py-4.5">
          <span className="text-sm font-semibold">
            Potwierdzenie poszło na lakasosnowa@example.org
          </span>
          <span className="text-[13px] leading-relaxed text-text-link">
            Jeśli nie dotrze w ciągu kilkunastu minut, sprawdźcie folder ze spamem. Oferta
            jest złożona niezależnie od tego, czy mail dojdzie: liczy się wpis w systemie z
            godziny 11:02.
          </span>
        </div>
      </div>
    </ScreenFrame>
  );
}
