import { cardClassName } from "@/components/ui/styles";
import { formatAmount } from "@/lib/format";
import type { ApplicationList } from "@/lib/operator-applications";

/**
 * Four figures above the list of a competition's applications: how many came
 * in, how far formal evaluation has got, how many wait on a correction, and
 * how the requested money compares with the pool. All of them are counted from
 * the list the table below already holds, so the tiles and the table can never
 * tell two different stories.
 */
export function ApplicationSummary({ list }: { list: ApplicationList }) {
  const { applications } = list;
  const decided = applications.filter((item) => item.formal === "Passed" || item.formal === "Failed").length;
  const returned = applications.filter((item) => item.status === "Returned").length;
  const share = applications.length === 0 ? 0 : Math.round((decided / applications.length) * 100);

  return (
    <dl className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <Tile term="Złożone wnioski">
        <Figure>{applications.length}</Figure>
      </Tile>

      <Tile term="Ocena formalna zakończona">
        <Figure>
          {decided} z {applications.length}
        </Figure>
        {/* Decoration: the figures above already say it in words. */}
        <span aria-hidden="true" className="mt-2 block h-1.5 overflow-hidden rounded-pill bg-border-muted">
          <span className="block h-full bg-brand-accent" style={{ width: `${share}%` }} />
        </span>
      </Tile>

      <Tile term="Zwrócone do poprawy">
        <Figure>{returned}</Figure>
      </Tile>

      <Tile term="Wnioskowane razem">
        <Figure>{formatAmount(list.requestedTotal)}</Figure>
        <span className="text-xs text-text-muted">
          {list.totalPoolAmount === null
            ? "Pula konkursu nie jest ustawiona."
            : `Pula konkursu: ${formatAmount(list.totalPoolAmount)}.`}
        </span>
      </Tile>
    </dl>
  );
}

function Tile({ term, children }: { term: string; children: React.ReactNode }) {
  return (
    <div className={`${cardClassName} flex flex-col gap-1 px-5 py-4`}>
      <dt className="text-sm text-text-muted">{term}</dt>
      <dd className="flex flex-col">{children}</dd>
    </div>
  );
}

function Figure({ children }: { children: React.ReactNode }) {
  return <span className="font-heading text-3xl font-extrabold leading-tight lining-nums tabular-nums">{children}</span>;
}
