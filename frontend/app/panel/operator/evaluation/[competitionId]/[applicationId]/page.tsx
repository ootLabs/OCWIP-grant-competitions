"use client";

import Link from "next/link";
import { use, useEffect, useState } from "react";

import { EvaluationView } from "@/components/evaluation/evaluation-view";
import { OfferView } from "@/components/offer-view";
import { apiErrorMessage } from "@/lib/api-client";
import { formatFileSize } from "@/lib/format";
import type { FormAnswers } from "@/lib/forms/answer-types";
import type { FormDocument } from "@/lib/forms/document-types";
import {
  attachmentUrl,
  entityTypeLabels,
  fetchSubmittedApplication,
  type SubmittedApplication,
} from "@/lib/operator-applications";
import { fetchApplicationEvaluations, type ApplicationEvaluationItem } from "@/lib/operator-evaluation";

import { operatorPanelRoot } from "../../../navigation";
import { FormalCard } from "./formal-card";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error"; readonly message: string }
  | { readonly status: "ready"; readonly offer: SubmittedApplication; readonly items: ApplicationEvaluationItem[] };

/**
 * One application in the evaluation (T-41a): the formal card the operator's
 * staff fill in, every expert's merit card read only with the expert's name,
 * and the application itself under them, on one screen.
 */
export default function ApplicationEvaluationPage({
  params,
}: {
  params: Promise<{ competitionId: string; applicationId: string }>;
}) {
  const { competitionId, applicationId } = use(params);
  const [load, setLoad] = useState<Load>({ status: "loading" });

  useEffect(() => {
    let current = true;

    Promise.all([fetchSubmittedApplication(competitionId, applicationId), fetchApplicationEvaluations(applicationId)])
      .then(([offer, items]) => {
        if (current) setLoad({ status: "ready", offer, items });
      })
      .catch((failure: unknown) => {
        if (current) {
          setLoad({ status: "error", message: apiErrorMessage(failure, "Nie udało się pobrać oceny wniosku.") });
        }
      });

    return () => {
      current = false;
    };
  }, [competitionId, applicationId]);

  const back = `${operatorPanelRoot}/evaluation/${competitionId}`;

  return (
    <div className="flex flex-col gap-8">
      <p className="text-sm">
        <Link className="underline" href={back}>
          Wróć do listy rankingowej
        </Link>
      </p>

      {load.status === "loading" ? <p className="text-sm">Wczytywanie wniosku…</p> : null}
      {load.status === "error" ? (
        <p role="alert" className="text-sm">
          {load.message}
        </p>
      ) : null}

      {load.status === "ready" ? <Ready applicationId={applicationId} offer={load.offer} items={load.items} /> : null}
    </div>
  );
}

function Ready({
  applicationId,
  offer,
  items,
}: {
  applicationId: string;
  offer: SubmittedApplication;
  items: ApplicationEvaluationItem[];
}) {
  const formal = items.find((item) => item.evaluation.stage === "Formal")?.evaluation ?? null;
  const merit = items.filter((item) => item.evaluation.stage === "Merit");

  return (
    <>
      <h1 className="text-2xl">
        Ocena wniosku {offer.number}: {offer.entityName}
      </h1>

      <FormalCard applicationId={applicationId} existing={formal} />

      <section aria-labelledby="oceny-merytoryczne" className="flex flex-col gap-3">
        <h2 id="oceny-merytoryczne" className="text-xl">
          Oceny merytoryczne ekspertów
        </h2>
        {merit.length === 0 ? (
          <p className="text-sm">Żaden ekspert nie otworzył jeszcze karty tego wniosku.</p>
        ) : (
          merit.map((item) => (
            <EvaluationView key={item.evaluation.id} evaluation={item.evaluation} author={item.author} />
          ))
        )}
      </section>

      <section aria-labelledby="wniosek" className="flex flex-col gap-4">
        <h2 id="wniosek" className="text-xl">
          Wniosek
        </h2>
        <p className="text-sm">Rodzaj wnioskodawcy: {entityTypeLabels[offer.entityType]}.</p>
        <OfferView
          document={offer.definition as FormDocument}
          answers={offer.answers as FormAnswers}
          applicant={offer.entityType}
        />
        <h3 className="text-lg">Załączniki</h3>
        {offer.attachments.length === 0 ? (
          <p className="text-sm">Wniosek nie ma załączników.</p>
        ) : (
          <ul className="flex flex-col gap-1 text-sm">
            {offer.attachments.map((attachment) => (
              <li key={attachment.id}>
                <a className="underline" href={attachmentUrl(attachment.id)}>
                  {attachment.fileName}
                </a>{" "}
                ({formatFileSize(attachment.sizeInBytes)})
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
