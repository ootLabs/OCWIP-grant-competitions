import {
  declarationLabels,
  type CompetitionAssignment,
  type DeclarationRow,
  type ReviewerSummary,
} from "@/lib/operator-evaluation";

/**
 * Every expert with what they have in this competition (T-41, report step
 * 5.1): the assigned applications and the impartiality declaration. An expert
 * with nothing assigned is listed too, because that is who the operator is
 * about to give work to.
 */
export function ExpertsTable({
  reviewers,
  declarations,
  assignments,
  invited = new Set<string>(),
  onWithdraw,
}: {
  reviewers: readonly ReviewerSummary[];
  declarations: readonly DeclarationRow[];
  assignments: readonly CompetitionAssignment[];
  /** Accounts made by an invitation whose owner has not set a password yet (R-44). */
  invited?: ReadonlySet<string>;
  /** Withdraws the appointment; the backend refuses while applications are assigned. */
  onWithdraw?: (reviewerId: string) => void;
}) {
  const declared = new Map(declarations.map((row) => [row.reviewerId, row]));

  return (
    <table className="w-full border-collapse text-sm">
      <caption className="sr-only">
        Eksperci, przypisane im wnioski i stan deklaracji bezstronności
      </caption>
      <thead>
        <tr className="text-left">
          {[
            "Ekspert",
            "Adres e-mail",
            "Przypisane wnioski",
            "Deklaracja",
            "Powód odmowy",
            ...(onWithdraw ? ["Działania"] : []),
          ].map((heading) => (
            <th
              key={heading}
              scope="col"
              className="border-b border-border px-2 py-1"
            >
              {heading}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {reviewers.map((reviewer) => {
          const declaration = declared.get(reviewer.id);
          const count = assignments.filter(
            (assignment) => assignment.reviewerId === reviewer.id,
          ).length;
          return (
            <tr key={reviewer.id}>
              <td className="border-b border-border-muted px-2 py-1">
                {reviewer.name || reviewer.email}
                {invited.has(reviewer.id) ? (
                  <span className="block text-xs text-text-muted">zaproszenie wysłane, hasło jeszcze nieustawione</span>
                ) : null}
              </td>
              <td className="border-b border-border-muted px-2 py-1">
                {reviewer.email}
              </td>
              <td className="border-b border-border-muted px-2 py-1">
                {count}
              </td>
              <td className="border-b border-border-muted px-2 py-1">
                {declaration === undefined
                  ? "Brak przypisań w konkursie"
                  : declarationLabels[declaration.status]}
              </td>
              <td className="border-b border-border-muted px-2 py-1">
                {declaration?.refusalReason ?? ""}
              </td>
              {onWithdraw ? (
                <td className="border-b border-border-muted px-2 py-1">
                  <button
                    type="button"
                    className="underline disabled:no-underline disabled:opacity-50"
                    disabled={count > 0}
                    title={count > 0 ? "Najpierw cofnij przydziały tej osoby" : undefined}
                    onClick={() => onWithdraw(reviewer.id)}
                  >
                    Odwołaj
                  </button>
                </td>
              ) : null}
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
