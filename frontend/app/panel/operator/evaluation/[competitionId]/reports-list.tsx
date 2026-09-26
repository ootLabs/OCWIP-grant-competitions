"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { formatMoment } from "@/lib/format";
import { fetchCompetitionReports, reportStatusLabels, type ReportListItem } from "@/lib/reports";

import { operatorPanelRoot } from "../../navigation";

/** The reports of a competition (T-50a), each leading to its own screen. */
export function ReportsList({ competitionId }: { competitionId: string }) {
  const [reports, setReports] = useState<ReportListItem[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let current = true;
    fetchCompetitionReports(competitionId)
      .then((list) => {
        if (current) setReports(list);
      })
      .catch(() => {
        if (current) setFailed(true);
      });
    return () => {
      current = false;
    };
  }, [competitionId]);

  if (failed) return <p className="text-sm">Nie udało się pobrać sprawozdań.</p>;
  if (reports === null) return <p className="text-sm">Wczytywanie sprawozdań…</p>;
  if (reports.length === 0) {
    return <p className="text-sm">Nikt jeszcze nie zaczął sprawozdania. Sprawozdanie zaczyna wnioskodawca z dofinansowanym wnioskiem.</p>;
  }

  return (
    <table className="w-full border-collapse text-sm">
      <caption className="sr-only">Sprawozdania konkursu i ich stan</caption>
      <thead>
        <tr className="text-left">
          {["Wniosek", "Wnioskodawca", "Stan", "Złożone"].map((heading) => (
            <th key={heading} scope="col" className="border-b border-border px-2 py-1">
              {heading}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {reports.map((report) => (
          <tr key={report.id}>
            <td className="border-b border-border-muted px-2 py-1">
              <Link className="underline" href={`${operatorPanelRoot}/evaluation/${competitionId}/reports/${report.id}`}>
                {report.applicationNumber ?? "bez numeru"}
              </Link>
            </td>
            <td className="border-b border-border-muted px-2 py-1">{report.entityName}</td>
            <td className="border-b border-border-muted px-2 py-1">{reportStatusLabels[report.status]}</td>
            <td className="border-b border-border-muted px-2 py-1">
              {report.submittedAt ? formatMoment(report.submittedAt) : ""}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
