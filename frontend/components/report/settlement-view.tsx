import { formatAmount } from "@/lib/format";
import { costRowLabel, type Report, type ReportSettlement } from "@/lib/reports";

/**
 * The settlement of the grant (T-50b), counted by the server from the
 * report's budget and the operator's review: the totals, and every cost the
 * operator did not accept with its reason, next to the row it concerns.
 * The same view for the applicant and the operator.
 */
export function SettlementView({ report, settlement }: { report: Report; settlement: ReportSettlement }) {
  const refused = settlement.rows.filter((row) => Number(row.refused) > 0);
  const final = report.status === "Accepted";

  return (
    <section aria-labelledby="rozliczenie" className="flex flex-col gap-3 text-sm">
      <h2 id="rozliczenie" className="text-xl">
        Rozliczenie dotacji
      </h2>
      {final ? null : <p>Wyliczenie wstępne: ostateczne jest po przyjęciu sprawozdania.</p>}
      <dl className="grid grid-cols-[auto_auto] justify-start gap-x-6 gap-y-1">
        <dt>Dotacja przyznana</dt>
        <dd>{settlement.awardedGrant === null ? "brak kwoty" : formatAmount(settlement.awardedGrant)}</dd>
        <dt>Wydatki z dotacji</dt>
        <dd>{formatAmount(settlement.grantSpent)}</dd>
        <dt>Koszty nieuznane</dt>
        <dd>{formatAmount(settlement.refused)}</dd>
        <dt>Koszty uznane</dt>
        <dd>{formatAmount(settlement.accepted)}</dd>
        <dt className="font-medium">Kwota do zwrotu</dt>
        <dd className="font-medium">{settlement.refund === null ? "nie da się wyliczyć" : formatAmount(settlement.refund)}</dd>
      </dl>
      {refused.length > 0 ? (
        <table className="w-full border-collapse">
          <caption className="text-left font-medium">Koszty nieuznane</caption>
          <thead>
            <tr>
              <th scope="col" className="border-b border-border-control py-1 text-left">Pozycja budżetu</th>
              <th scope="col" className="border-b border-border-control py-1 text-right">Wydatek z dotacji</th>
              <th scope="col" className="border-b border-border-control py-1 text-right">Nieuznane</th>
              <th scope="col" className="border-b border-border-control py-1 pl-4 text-left">Powód</th>
            </tr>
          </thead>
          <tbody>
            {refused.map((row) => (
              <tr key={String(row.row)}>
                <th scope="row" className="py-1 text-left font-normal">{costRowLabel(report, settlement.budgetKey, Number(row.row))}</th>
                <td className="py-1 text-right">{formatAmount(row.spent)}</td>
                <td className="py-1 text-right">{formatAmount(row.refused)}</td>
                <td className="py-1 pl-4">{row.reason}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <p>Wszystkie koszty są uznane.</p>
      )}
    </section>
  );
}
