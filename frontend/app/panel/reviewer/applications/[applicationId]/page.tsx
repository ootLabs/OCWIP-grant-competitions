"use client";

import Link from "next/link";
import { use, useEffect, useState } from "react";

import { OfferView } from "@/components/offer-view";
import { apiErrorMessage } from "@/lib/api-client";
import {
  fetchApplication,
  fetchApplicationForm,
  fetchAttachments,
  type Application,
  type ApplicationForm,
  type Attachment,
} from "@/lib/applicant-applications";
import { formatFileSize } from "@/lib/format";
import type { FormAnswers } from "@/lib/forms/answer-types";
import { attachmentUrl } from "@/lib/operator-applications";
import { openMeritCard, type Evaluation } from "@/lib/reviewer-work";

import { reviewerPanelRoot } from "../../navigation";
import { EvaluationWorkspace } from "./evaluation-workspace";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error"; readonly message: string }
  | {
      readonly status: "ready";
      readonly application: Application;
      readonly form: ApplicationForm;
      readonly attachments: Attachment[];
      readonly evaluation: Evaluation;
    };

/**
 * One application to evaluate (T-40, report step 5.4): the card on top and
 * the whole application under it, scrolled, never on a second screen. Every
 * read goes through the assignment on the server; an expert who is not
 * assigned gets the refusal the API gives.
 */
export default function ReviewerApplicationPage({
  params,
}: {
  params: Promise<{ applicationId: string }>;
}) {
  const { applicationId } = use(params);
  const [load, setLoad] = useState<Load>({ status: "loading" });

  useEffect(() => {
    let current = true;

    Promise.all([
      fetchApplication(applicationId),
      fetchApplicationForm(applicationId),
      fetchAttachments(applicationId),
      openMeritCard(applicationId),
    ])
      .then(([application, form, attachments, evaluation]) => {
        if (current) setLoad({ status: "ready", application, form, attachments, evaluation });
      })
      .catch((error: unknown) => {
        if (current) {
          setLoad({
            status: "error",
            message: apiErrorMessage(error, "Nie udało się otworzyć wniosku do oceny."),
          });
        }
      });

    return () => {
      current = false;
    };
  }, [applicationId]);

  return (
    <div className="flex flex-col gap-8">
      <p className="text-sm">
        <Link className="underline" href={reviewerPanelRoot}>
          Wróć do listy wniosków
        </Link>
      </p>

      {load.status === "loading" ? <p className="text-sm">Wczytywanie wniosku…</p> : null}

      {load.status === "error" ? (
        <p role="alert" className="text-sm">
          {load.message}
        </p>
      ) : null}

      {load.status === "ready" ? (
        <>
          <h1 className="text-2xl">Ocena wniosku {load.application.number ?? ""}</h1>

          <EvaluationWorkspace evaluation={load.evaluation} />

          <section aria-labelledby="wniosek" className="flex flex-col gap-4">
            <h2 id="wniosek" className="text-xl">
              Wniosek
            </h2>
            <OfferView document={load.form.document} answers={load.application.answers as FormAnswers} />

            <h3 className="text-lg">Załączniki</h3>
            {load.attachments.length === 0 ? (
              <p className="text-sm">Wniosek nie ma załączników.</p>
            ) : (
              <ul className="flex flex-col gap-1 text-sm">
                {load.attachments.map((attachment) => (
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
      ) : null}
    </div>
  );
}
