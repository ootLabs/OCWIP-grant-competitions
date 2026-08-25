import { StatusChip } from "@/components/StatusChip";

export const WIZARD_STEPS = [
  {
    label: "Podmiot",
    question: "Czy dane podmiotu się zgadzają?",
    helper:
      "Pobraliśmy je z waszego konta. Jeśli coś się zmieniło, poprawcie teraz, bo te dane trafią później do umowy.",
  },
  {
    label: "Zadanie",
    question: "Jak nazywa się wasze zadanie?",
    helper: "Krótko i konkretnie. Podajcie też, gdzie i kiedy chcecie je zrealizować.",
  },
  {
    label: "Opis",
    question: "Opiszcie, co chcecie zrobić",
    helper:
      "Napiszcie tak, jakbyście tłumaczyli sąsiadowi. Komisja czyta kilkadziesiąt wniosków, więc konkret waży więcej niż styl.",
  },
  {
    label: "Odbiorcy",
    question: "Do kogo skierowane jest zadanie?",
    helper: "Kto skorzysta i ile mniej więcej osób. Nie musi być co do jednego.",
  },
  {
    label: "Budżet",
    question: "Ile to będzie kosztować?",
    helper:
      "Wypiszcie pozycje budżetu. Pilnujemy limitu na bieżąco, więc nie dowiecie się o przekroczeniu dopiero przy wysyłce.",
  },
  {
    label: "Załączniki",
    question: "Co dołączacie do oferty?",
    helper:
      "Dwa załączniki są w tym konkursie wymagane. Pozostałe dodajcie, jeśli uważacie, że pomogą komisji zrozumieć zadanie.",
  },
];

export const DESCRIPTION_LIMIT = 2000;

function FieldValue({ children }: { children: React.ReactNode }) {
  return (
    <span className="block rounded-[var(--radius-sm)] border border-border px-3.5 py-3 text-base">
      {children}
    </span>
  );
}

function ReadRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col gap-1">
      <span className="text-[13px] text-text-link">{label}</span>
      <span className="text-base">{value}</span>
    </div>
  );
}

const BUDGET_ROWS = [
  { name: "Ławki parkowe, 2 sztuki", amount: "3 200 zł" },
  { name: "Sadzonki żywopłotu i ziemia", amount: "1 850 zł" },
  { name: "Narzędzia ogrodnicze", amount: "940 zł" },
  { name: "Wynagrodzenie animatora, 2 spotkania", amount: "2 400 zł" },
];

type WizardStepBodyProps = {
  step: number;
  description: string;
  onDescriptionChange: (value: string) => void;
};

export function WizardStepBody({
  step,
  description,
  onDescriptionChange,
}: WizardStepBodyProps) {
  if (step === 0) {
    return (
      <div className="flex flex-col gap-3.5 rounded-[var(--radius-sm)] bg-surface-muted p-5">
        <ReadRow label="Typ podmiotu" value="Grupa nieformalna, trzy osoby fizyczne" />
        <ReadRow
          label="Osoby reprezentujące"
          value="Anna Wieczorek, Marek Sobota, Julia Pawlak"
        />
        {/* Address is personal data: flagged so encryption work does not miss it. */}
        <ReadRow label="Adres korespondencyjny" value="ul. Sosnowa 14/3, 45-062 Opole" />
        <div className="flex flex-col gap-1">
          <span className="text-[13px] text-text-link">NIP</span>
          <span className="text-base text-text-link">
            Nie dotyczy. Grupa nieformalna nie ma NIP-u i to nie jest brak w waszym
            wniosku.
          </span>
        </div>
        <span className="inline-flex min-h-[44px] w-fit items-center rounded-[var(--radius-sm)] border border-border bg-bg px-5 text-sm font-semibold">
          Zmień dane podmiotu
        </span>
      </div>
    );
  }

  if (step === 1) {
    return (
      <div className="flex flex-col gap-4.5">
        <div className="flex flex-col gap-1.5">
          <span className="text-sm font-semibold">Tytuł zadania</span>
          <FieldValue>Zielone podwórko na Sosnowej</FieldValue>
          <span className="text-[13px] leading-relaxed text-text-link">
            Ta nazwa trafi do umowy i na listę wyników, więc niech będzie zrozumiała dla
            kogoś z zewnątrz.
          </span>
        </div>
        <div className="flex flex-col gap-1.5">
          <span className="text-sm font-semibold">Miejsce realizacji</span>
          <FieldValue>
            Opole, osiedle Armii Krajowej, podwórko przy ul. Sosnowej 12 do 16
          </FieldValue>
        </div>
        <div className="flex flex-col gap-4 sm:flex-row">
          <div className="flex grow basis-0 flex-col gap-1.5">
            <span className="text-sm font-semibold">Początek</span>
            <FieldValue>1 marca 2026</FieldValue>
          </div>
          <div className="flex grow basis-0 flex-col gap-1.5">
            <span className="text-sm font-semibold">Zakończenie</span>
            <FieldValue>30 czerwca 2026</FieldValue>
          </div>
        </div>
      </div>
    );
  }

  if (step === 2) {
    const left = DESCRIPTION_LIMIT - description.length;
    return (
      <div className="flex flex-col gap-3.5">
        <div className="flex flex-col gap-1.5">
          <label className="sr-only" htmlFor="wizard-description">
            Opis zadania
          </label>
          <textarea
            id="wizard-description"
            value={description}
            onChange={(event) => onDescriptionChange(event.target.value)}
            className="min-h-[172px] w-full resize-y rounded-[var(--radius-sm)] border border-border p-3.5 font-body text-base leading-relaxed text-text"
          />
          <div className="flex flex-wrap items-baseline gap-3">
            <span className="text-[13px] text-text-link">
              Piszcie spokojnie, zapisujemy w tle.
            </span>
            <span className="grow" />
            <span
              className={`text-xs ${left >= 0 ? "text-text-link" : "text-brand-accent-text"}`}
            >
              {left >= 0
                ? `Pozostało ${left} znaków`
                : `Za dużo o ${Math.abs(left)} znaków`}
            </span>
          </div>
        </div>
        <div className="flex flex-col gap-2 rounded-[var(--radius-sm)] bg-surface-muted px-4.5 py-4">
          <span className="text-[13px] font-semibold">Warto, żeby znalazło się tu</span>
          <span className="text-[13px] leading-relaxed text-text-link">
            co po kolei zrobicie, kto to zrobi, co zostanie na miejscu po zakończeniu
            zadania
          </span>
        </div>
      </div>
    );
  }

  if (step === 3) {
    return (
      <div className="flex flex-col gap-4.5">
        <div className="flex flex-col gap-1.5">
          <span className="text-sm font-semibold">Kto skorzysta</span>
          <span className="block min-h-[108px] rounded-[var(--radius-sm)] border border-border p-3.5 text-base leading-relaxed">
            Mieszkańcy trzech klatek przy ul. Sosnowej, w większości seniorzy oraz rodziny
            z małymi dziećmi. Podwórko jest wspólne dla całego kwartału, więc korzystać
            będą też sąsiedzi z ul. Modrzewiowej.
          </span>
        </div>
        <div className="flex max-w-[280px] flex-col gap-1.5">
          <span className="text-sm font-semibold">Szacowana liczba odbiorców</span>
          <FieldValue>40</FieldValue>
          <span className="text-[13px] leading-relaxed text-text-link">
            Szacunek wystarczy, komisja chce zrozumieć skalę.
          </span>
        </div>
      </div>
    );
  }

  if (step === 4) {
    return (
      <div className="flex flex-col gap-3">
        <div className="flex flex-col overflow-hidden rounded-[var(--radius-sm)] border border-border">
          {BUDGET_ROWS.map((row) => (
            <div
              key={row.name}
              className="flex items-center gap-3 border-b border-border-muted px-3.5 py-2.5"
            >
              <span className="grow text-[15px]">{row.name}</span>
              <span className="text-[15px] font-semibold">{row.amount}</span>
            </div>
          ))}
          <div className="flex items-center gap-3 bg-surface-muted px-3.5 py-3">
            <span className="grow text-[15px] font-semibold">Razem</span>
            <span className="text-[15px] font-semibold">8 390 zł</span>
            <span className="text-[13px] text-text-link">z 9 000 zł</span>
          </div>
        </div>
        <span className="text-[13px] leading-relaxed text-text-link">
          Pełny widok budżetu z pilnowaniem limitu stoi na osobnym ekranie.
        </span>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-2.5">
      <div className="flex flex-wrap items-center gap-3.5 rounded-[var(--radius-sm)] border border-border px-4 py-3.5">
        <span className="flex grow flex-col gap-1">
          <span className="text-[15px] font-semibold">Oświadczenie o niekaralności</span>
          <span className="text-[13px] leading-relaxed text-text-link">
            wymagany, PDF, 240 kB, dodany 28 września
          </span>
        </span>
        <StatusChip variant="accent">Dodany</StatusChip>
      </div>

      <div className="flex flex-wrap items-center gap-3.5 rounded-[var(--radius-sm)] border-2 border-brand-accent px-4 py-3.5">
        <span className="flex grow flex-col gap-1">
          <span className="text-[15px] font-semibold">Zgoda właściciela terenu</span>
          <span className="text-[13px] leading-relaxed text-text-link">
            wymagany, brakuje. Bez niego nie da się złożyć oferty.
          </span>
        </span>
        <span className="inline-flex min-h-[44px] items-center rounded-[var(--radius-sm)] bg-brand-accent px-4.5 text-sm font-semibold text-bg">
          Dodaj plik
        </span>
      </div>

      <div className="flex flex-wrap items-center gap-3.5 rounded-[var(--radius-sm)] border border-border px-4 py-3.5">
        <span className="flex grow flex-col gap-1">
          <span className="text-[15px] font-semibold">Zdjęcia podwórka</span>
          <span className="text-[13px] leading-relaxed text-text-link">
            nieobowiązkowy, 2 pliki, 1,8 MB
          </span>
        </span>
        <StatusChip>Dodane</StatusChip>
      </div>
    </div>
  );
}
