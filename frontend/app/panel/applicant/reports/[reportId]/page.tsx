"use client";

import Link from "next/link";
import { use, useEffect, useState } from "react";

import { OfferView } from "@/components/offer-view";
import { ReportWorkspace } from "@/components/report/report-workspace";
import { apiErrorMessage } from "@/lib/api-client";
import { formatMoment } from "@/lib/format";
import { fetchReport, reportFormOf, reportStatusLabels, type Report } from "@/lib/reports";

import { applicantPanelRoot } from "../../navigation";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error"; readonly message: string }
  | { readonly status: "ready"; readonly report: Report };

/**
 * The applicant's report (T-50a): editable while in preparation or sent back,
 * read only once submitted or accepted. A return reason stays on top until
 * the next submission, so it is read before anything is changed.
 */
export default function ApplicantReportPage({ params }: { params: Promise<{ reportId: string }> }) {
  const { reportId } = use(params);
  const [load, setLoad] = useState<Load>({ status: "loading" });

  useEffect(() => {
    let current = true;
    fetchReport(reportId)
      .then((report) => {
        if (current) setLoad({ status: "ready", report });
      })
      .catch((failure: unknown) => {
        if (current) setLoad({ status: "error", message: apiErrorMessage(failure, "Nie udało się pobrać sprawozdania.") });
      });
    return () => {
      current = false;
    };
  }, [reportId]);

  return (
    <div className="flex flex-col gap-6">
      <p className="text-sm">
        <Link className="underline" href={applicantPanelRoot}>
          Wróć do listy wniosków
        </Link>
      </p>

      {load.status === "loading" ? <p className="text-sm">Wczytywanie sprawozdania…</p> : null}
      {load.status === "error" ? (
        <p role="alert" className="text-sm">
          {load.message}
        </p>
      ) : null}

      {load.status === "ready" ? <Ready report={load.report} onChange={(report) => setLoad({ status: "ready", report })} /> : null}
    </div>
  );
}

function Ready({ report, onChange }: { report: Report; onChange: (report: Report) => void }) {
  const editable = report.status === "Draft" || report.status === "Returned";
  const form = reportFormOf(report);

  return (
    <>
      <div>
        <h1 className="text-2xl">Sprawozdanie z wniosku {report.applicationNumber ?? ""}</h1>
        <p className="text-sm">Stan: {reportStatusLabels[report.status]}.</p>
      </div>

      {report.status === "Returned" && report.returnReason ? (
        <div role="alert" className="rounded-sm border border-border-control p-4 text-sm">
          <p className="font-medium">Operator zwrócił sprawozdanie do poprawy:</p>
          <p>{report.returnReason}</p>
        </div>
      ) : null}

      {editable ? (
        <ReportWorkspace report={report} onSubmitted={onChange} />
      ) : (
        <section className="flex flex-col gap-4">
          <p className="text-sm">
            {report.status === "Accepted" && report.acceptedAt
              ? `Sprawozdanie przyjęte ${formatMoment(report.acceptedAt)}.`
              : report.submittedAt
                ? `Sprawozdanie złożone ${formatMoment(report.submittedAt)}. Czeka na sprawdzenie przez operatora.`
                : null}
          </p>
          <OfferView document={form.document} answers={form.answers} applicant={form.applicant} />
        </section>
      )}
    </>
  );
}
