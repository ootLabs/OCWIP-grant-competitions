import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

import { competitionPath, fetchPublicResults } from "@/lib/competitions";
import { formatAmount, formatMoment, timeZoneLabel } from "@/lib/format";

type PageProps = { params: Promise<{ id: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const results = await fetchPublicResults((await params).id);
  return results === null
    ? { title: "Nie znaleziono wyników" }
    : { title: `Wyniki: ${results.competitionTitle}` };
}

const score = (value: number | string | null | undefined) =>
  value === null || value === undefined ? "" : String(Number(value)).replace(".", ",");

/**
 * The published results (T-42a, report: the list published without being
 * copied by hand). Rendered on the server, so it works without JavaScript and
 * in a link preview. Only the funded applications and the reserve list; the
 * page does not exist before the operator approves the results.
 */
export default async function ResultsPage({ params }: PageProps) {
  const id = (await params).id;
  const results = await fetchPublicResults(id);

  if (results === null) {
    notFound();
  }

  const funded = results.rows.filter((row) => row.status === "Funded");
  const reserve = results.rows.filter((row) => row.status === "Reserve");

  return (
    <article className="flex flex-col gap-6">
      <header className="flex flex-col gap-2">
        <p className="text-sm">
          <Link className="underline" href={competitionPath(id)}>
            Konkurs nr {results.competitionNumber}
          </Link>
        </p>
        <h1 className="text-3xl">Wyniki konkursu: {results.competitionTitle}</h1>
        <p className="text-sm">
          Wyniki zatwierdzone {formatMoment(results.approvedAt)} ({timeZoneLabel()}).
        </p>
      </header>

      <ResultsTable title="Wnioski dofinansowane" rows={funded} withGrant />
      <ResultsTable title="Lista rezerwowa" rows={reserve} withGrant={false} />
    </article>
  );
}

function ResultsTable({
  title,
  rows,
  withGrant,
}: {
  title: string;
  rows: readonly {
    rank?: number | string | null;
    number: string | null;
    entityName: string;
    projectTitle: string | null;
    totalScore?: number | string | null;
    awardedGrant?: number | string | null;
  }[];
  withGrant: boolean;
}) {
  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-xl">{title}</h2>
      {rows.length === 0 ? (
        <p className="text-sm">Brak wniosków w tej części listy.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="text-left">
                <th scope="col" className="border-b border-border px-2 py-1">Miejsce</th>
                <th scope="col" className="border-b border-border px-2 py-1">Numer</th>
                <th scope="col" className="border-b border-border px-2 py-1">Wnioskodawca</th>
                <th scope="col" className="border-b border-border px-2 py-1">Tytuł projektu</th>
                <th scope="col" className="border-b border-border px-2 py-1">Punkty</th>
                {withGrant ? (
                  <th scope="col" className="border-b border-border px-2 py-1 text-right">Kwota dotacji</th>
                ) : null}
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.number ?? row.entityName}>
                  <td className="border-b border-border-muted px-2 py-1">{row.rank ?? ""}</td>
                  <td className="border-b border-border-muted px-2 py-1">{row.number ?? ""}</td>
                  <td className="border-b border-border-muted px-2 py-1">{row.entityName}</td>
                  <td className="border-b border-border-muted px-2 py-1">{row.projectTitle ?? ""}</td>
                  <td className="border-b border-border-muted px-2 py-1">{score(row.totalScore)}</td>
                  {withGrant ? (
                    <td className="border-b border-border-muted px-2 py-1 text-right">
                      {row.awardedGrant === null || row.awardedGrant === undefined ? "" : formatAmount(row.awardedGrant)}
                    </td>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
