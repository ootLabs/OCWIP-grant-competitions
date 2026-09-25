"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import { OfferView } from "@/components/offer-view";
import { ApiError } from "@/lib/api-client";
import { formatFileSize, formatMoment } from "@/lib/format";
import type { FormAnswers } from "@/lib/forms/answer-types";
import type { FormDocument } from "@/lib/forms/document-types";
import {
  applicationStatusLabels,
  attachmentUrl,
  entityTypeLabels,
  fetchSubmittedApplication,
  type SubmittedApplication,
} from "@/lib/operator-applications";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "missing" }
  | { readonly status: "ready"; readonly offer: SubmittedApplication };

/**
 * One submitted offer as the operator opens it from the list (T-35): who
 * submitted it and when, the technical block (form version and checksum,
 * D15), the attachments, and the answers in the form version it was filled
 * in on. A draft does not open here: the backend answers 404 for it, the
 * same as for an address that leads nowhere.
 */
export default function SubmittedApplicationPage() {
  const { competitionId, applicationId } = useParams<{
    competitionId: string;
    applicationId: string;
  }>();
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);
  const back = `/panel/operator/applications/${competitionId}`;

  useEffect(() => {
    let current = true;
    setLoad({ status: "loading" });

    fetchSubmittedApplication(competitionId, applicationId)
      .then((offer) => {
        if (current) {
          setLoad({ status: "ready", offer });
        }
      })
      .catch((error: unknown) => {
        if (current) {
          setLoad({
            status: error instanceof ApiError && error.status === 404 ? "missing" : "error",
          });
        }
      });

    return () => {
      current = false;
    };
  }, [competitionId, applicationId, attempt]);

  return (
    <section className="flex max-w-4xl flex-col gap-4">
      <Link href={back} className="text-sm underline">
        Lista wniosków
      </Link>

      {load.status === "loading" ? (
        <>
          <h1 className="text-2xl">Wniosek</h1>
          <p className="text-sm">Wczytywanie wniosku…</p>
        </>
      ) : null}

      {load.status === "error" ? (
        <>
          <h1 className="text-2xl">Wniosek</h1>
          <p className="text-sm">
            Nie udało się pobrać wniosku.{" "}
            <button
              type="button"
              className="underline"
              onClick={() => setAttempt((value) => value + 1)}
            >
              Spróbuj ponownie
            </button>
            .
          </p>
        </>
      ) : null}

      {load.status === "missing" ? (
        <>
          <h1 className="text-2xl">Wniosek</h1>
          <EmptyState
            title="W tym konkursie nie ma takiego złożonego wniosku"
            action={{ href: back, label: "Wróć do listy wniosków" }}
          >
            Na liście są tylko wnioski złożone. Wersja robocza, którą
            wnioskodawca dopiero wypełnia, nie jest jeszcze wnioskiem.
          </EmptyState>
        </>
      ) : null}

      {load.status === "ready" ? <Offer offer={load.offer} /> : null}
    </section>
  );
}

function Offer({ offer }: { offer: SubmittedApplication }) {
  return (
    <>
      <h1 className="text-2xl">
        Wniosek {offer.number}: {offer.entityName}
      </h1>

      <dl className="grid grid-cols-1 gap-x-6 gap-y-1 text-sm sm:grid-cols-[max-content_1fr]">
        <dt>Konkurs</dt>
        <dd>{offer.competitionTitle}</dd>
        <dt>Rodzaj wnioskodawcy</dt>
        <dd>{entityTypeLabels[offer.entityType]}</dd>
        <dt>Status</dt>
        <dd>{applicationStatusLabels[offer.status]}</dd>
        <dt>Data złożenia</dt>
        <dd>{formatMoment(offer.submittedAt)}</dd>
        <dt>Wersja formularza</dt>
        <dd>{offer.formVersion}</dd>
        <dt>Suma kontrolna</dt>
        <dd className="font-mono">{offer.checksum}</dd>
      </dl>

      <section className="flex flex-col gap-2">
        <h2 className="text-xl">Załączniki</h2>
        {offer.attachments.length === 0 ? (
          <p className="text-sm">Do wniosku nie dołączono plików.</p>
        ) : (
          <ul className="flex flex-col gap-1 text-sm">
            {offer.attachments.map((attachment) => (
              <li key={attachment.id}>
                <a href={attachmentUrl(attachment.id)} className="underline">
                  {attachment.fileName}
                </a>{" "}
                ({formatFileSize(attachment.sizeInBytes)})
              </li>
            ))}
          </ul>
        )}
      </section>

      <OfferView
        document={offer.definition as FormDocument}
        answers={offer.answers as FormAnswers}
      />
    </>
  );
}
