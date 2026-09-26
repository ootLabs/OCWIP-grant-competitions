"use client";

import Link from "next/link";
import { use, useEffect, useId, useState } from "react";

import { ConfirmDialog } from "@/components/confirm-dialog";
import { OfferView } from "@/components/offer-view";
import { apiErrorMessage } from "@/lib/api-client";
import { formatMoment } from "@/lib/format";
import { acceptReport, fetchReport, reportFormOf, reportStatusLabels, returnReport, type Report } from "@/lib/reports";

import { operatorPanelRoot } from "../../../../navigation";

/**
 * One report for the operator (T-50a): the whole report as the applicant
 * submitted it, with the application's values next to the execution, and the
 * two decisions on a submitted one: accept, or send back with a reason the
 * applicant will read.
 */
export default function OperatorReportPage({
  params,
}: {
  params: Promise<{ competitionId: string; reportId: string }>;
}) {
  const { competitionId, reportId } = use(params);
  const [report, setReport] = useState<Report | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let current = true;
    fetchReport(reportId)
      .then((loaded) => {
        if (current) setReport(loaded);
      })
      .catch((failure: unknown) => {
        if (current) setError(apiErrorMessage(failure, "Nie udało się pobrać sprawozdania."));
      });
    return () => {
      current = false;
    };
  }, [reportId]);

  return (
    <div className="flex flex-col gap-6">
      <p className="text-sm">
        <Link className="underline" href={`${operatorPanelRoot}/evaluation/${competitionId}`}>
          Wróć do oceny konkursu
        </Link>
      </p>
      {error !== null ? (
        <p role="alert" className="text-sm">
          {error}
        </p>
      ) : null}
      {report === null && error === null ? <p className="text-sm">Wczytywanie sprawozdania…</p> : null}
      {report !== null ? <Ready report={report} onChange={setReport} /> : null}
    </div>
  );
}

function Ready({ report, onChange }: { report: Report; onChange: (report: Report) => void }) {
  const form = reportFormOf(report);

  return (
    <>
      <div>
        <h1 className="text-2xl">
          Sprawozdanie z wniosku {report.applicationNumber ?? ""}: {report.entityName}
        </h1>
        <p className="text-sm">
          Stan: {reportStatusLabels[report.status]}
          {report.submittedAt ? `, złożone ${formatMoment(report.submittedAt)}` : ""}
          {report.acceptedAt ? `, przyjęte ${formatMoment(report.acceptedAt)}` : ""}.
        </p>
        {report.status === "Returned" && report.returnReason ? (
          <p className="text-sm">Powód zwrotu: {report.returnReason}</p>
        ) : null}
      </div>

      {report.status === "Submitted" ? <Decision report={report} onChange={onChange} /> : null}

      <OfferView document={form.document} answers={form.answers} applicant={form.applicant} />
    </>
  );
}

function Decision({ report, onChange }: { report: Report; onChange: (report: Report) => void }) {
  const [reason, setReason] = useState("");
  const [confirming, setConfirming] = useState<"accept" | "return" | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const reasonId = useId();

  async function decide() {
    setBusy(true);
    setError(null);
    try {
      onChange(confirming === "accept" ? await acceptReport(report.id) : await returnReport(report.id, reason));
      setConfirming(null);
    } catch (failure) {
      setError(apiErrorMessage(failure, "Nie udało się zapisać decyzji."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section aria-labelledby="decyzja" className="flex flex-col gap-3 text-sm">
      <h2 id="decyzja" className="text-xl">
        Decyzja
      </h2>
      <div>
        <button
          type="button"
          className="rounded-sm bg-brand-accent px-4 py-2 text-bg hover:bg-brand-accent-hover"
          onClick={() => setConfirming("accept")}
        >
          Przyjmij sprawozdanie
        </button>
      </div>
      <label htmlFor={reasonId} className="flex flex-col gap-1">
        Powód zwrotu (wnioskodawca go przeczyta)
        <textarea
          id={reasonId}
          rows={3}
          maxLength={2000}
          className="rounded-sm border border-border-control px-2 py-1"
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />
      </label>
      <div>
        <button
          type="button"
          className="underline disabled:opacity-40"
          disabled={reason.trim() === ""}
          onClick={() => setConfirming("return")}
        >
          Zwróć do poprawy
        </button>
      </div>
      {confirming !== null ? (
        <ConfirmDialog
          title={
            confirming === "accept"
              ? "Przyjąć sprawozdanie? Wnioskodawca nie będzie mógł go już zmienić."
              : "Zwrócić sprawozdanie do poprawy z podanym powodem?"
          }
          confirmLabel={confirming === "accept" ? "Przyjmij" : "Zwróć"}
          busyLabel="Zapisywanie…"
          busy={busy}
          error={error}
          onCancel={() => {
            setConfirming(null);
            setError(null);
          }}
          onConfirm={() => void decide()}
        />
      ) : null}
    </section>
  );
}
