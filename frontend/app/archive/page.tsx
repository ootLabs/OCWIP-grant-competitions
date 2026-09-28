import type { Metadata } from "next";
import Link from "next/link";

import { EmptyState } from "@/components/empty-state";
import { PublicFrame } from "@/components/public-frame";
import { fetchResultsArchive, resultsPath } from "@/lib/competitions";
import { formatAmount, formatMoment, timeZoneLabel } from "@/lib/format";

export const metadata: Metadata = {
  title: "Archiwum wyników | Generator konkursów OCWIP",
};

/**
 * The results archive (T-108, R-14, R-31): every resolved competition with
 * its funded projects, the latest approval first. Rendered on the server from
 * the published results, so nothing appears here before the operator
 * approves them. An informal group shows its own name only (RD3): the
 * backend never sends its members' names.
 */
export default async function ArchivePage() {
  const archive = await fetchResultsArchive();

  return (
    <PublicFrame>
      <div className="flex flex-col gap-8">
        <h1 className="text-3xl">Archiwum wyników</h1>

        {archive.length === 0 ? (
          <EmptyState title="Nie ma jeszcze rozstrzygniętych konkursów">
            Wyniki pojawią się tutaj po zatwierdzeniu listy rankingowej.
          </EmptyState>
        ) : (
          archive.map((entry) => (
            <section className="flex flex-col gap-2" key={entry.competitionId}>
              <h2 className="text-xl">
                Konkurs nr {entry.competitionNumber}: {entry.competitionTitle}
              </h2>
              <p className="text-sm">
                Wyniki zatwierdzone {formatMoment(entry.approvedAt)} ({timeZoneLabel()}).{" "}
                <Link className="text-text-link underline" href={resultsPath(entry.competitionId)}>
                  Pełne wyniki z listą rezerwową
                </Link>
              </p>
              {entry.projects.length === 0 ? (
                <p className="text-sm">Żaden projekt nie otrzymał dofinansowania.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full border-collapse text-sm">
                    <thead>
                      <tr className="text-left">
                        <th scope="col" className="border-b border-border px-2 py-1">Wnioskodawca</th>
                        <th scope="col" className="border-b border-border px-2 py-1">Tytuł projektu</th>
                        <th scope="col" className="border-b border-border px-2 py-1 text-right">Kwota dotacji</th>
                      </tr>
                    </thead>
                    <tbody>
                      {entry.projects.map((project, index) => (
                        <tr key={index}>
                          <td className="border-b border-border-muted px-2 py-1">{project.entityName}</td>
                          <td className="border-b border-border-muted px-2 py-1">{project.projectTitle ?? ""}</td>
                          <td className="border-b border-border-muted px-2 py-1 text-right">
                            {project.awardedGrant === null || project.awardedGrant === undefined
                              ? ""
                              : formatAmount(project.awardedGrant)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          ))
        )}
      </div>
    </PublicFrame>
  );
}
