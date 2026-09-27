import {
  entityTypeLabels,
  hasOrganisationCard,
  legalFormLabels,
  registerLabels,
  type EntityCard,
} from "@/lib/entity-card";

/**
 * The Podmiot card read back (T-93): on "Mój profil", on every application
 * after the first, and, from the copy taken at submission, on a submitted
 * application. Only what was filled in, as a definition list.
 */
export function EntityCardSummary({ card }: { card: EntityCard }) {
  const rows: [string, string | null | undefined][] = [
    ["Kto składa wniosek", entityTypeLabels[card.type]],
    [hasOrganisationCard(card.type) ? "Pełna nazwa" : "Nazwa grupy", card.name],
  ];

  if (hasOrganisationCard(card.type)) {
    rows.push(
      [
        "Forma prawna",
        card.legalForm === "Other"
          ? card.legalFormOther
          : card.legalForm
            ? legalFormLabels[card.legalForm]
            : null,
      ],
      ["Rejestr", card.register ? `${registerLabels[card.register]}: ${card.registerNumber ?? ""}` : null],
      ["NIP", card.nip],
      ["REGON", card.regon],
      ["Adres siedziby", card.address],
      ["Adres do korespondencji", card.correspondenceAddress],
      ["Telefon", card.phone],
      ["E-mail", card.email],
      ["Numer rachunku bankowego", card.bankAccount ? groupAccount(card.bankAccount) : null],
      [
        "Osoby uprawnione do reprezentowania",
        (card.representatives ?? [])
          .map((person) => `${person.firstName} ${person.lastName} (${person.function})`)
          .join(", "),
      ],
    );
  }

  return (
    <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[minmax(0,14rem)_1fr]">
      {rows
        .filter((row): row is [string, string] => Boolean(row[1]))
        .map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-text">{label}</dt>
            <dd className="break-words">{value}</dd>
          </div>
        ))}
    </dl>
  );
}

/** "73 1111 1111 ...", the way a bank statement prints it. */
function groupAccount(digits: string): string {
  return `${digits.slice(0, 2)} ${digits.slice(2).replace(/(\d{4})(?=\d)/g, "$1 ")}`;
}
