"use client";

import { useEffect, useRef, useState } from "react";
import { ApiError } from "@/lib/api-client";
import { publishFormDefinition } from "@/lib/forms/competition-forms";
import type { FormDocument } from "@/lib/forms/document-types";

type Status =
  | { readonly step: "idle" }
  | { readonly step: "confirming" }
  | { readonly step: "publishing" }
  | { readonly step: "error"; readonly message: string; readonly fieldErrors: Record<string, string[]> }
  | { readonly step: "published"; readonly versionNumber: number };

const GENERIC_ERROR = "Nie udało się opublikować formularza. Spróbuj ponownie.";

/**
 * The one explicit, confirmed action that turns a draft into a real,
 * versioned definition (T-27, T-25's POST). No native `window.confirm`:
 * nothing else in this app uses one, and it cannot say what publishing
 * actually does to the previous version the way an inline panel can.
 */
export function PublishPanel({
  competitionId,
  document,
  onPublished,
}: {
  competitionId: string;
  document: FormDocument;
  onPublished: (versionNumber: number) => void;
}) {
  const [status, setStatus] = useState<Status>({ step: "idle" });

  // Any edit after a publish or a rejection is a fresh attempt: without this,
  // "Opublikowano wersję 1" (or a stale field error) would sit there forever
  // and there would be no way back to the button to publish version 2.
  const previousDocument = useRef(document);
  useEffect(() => {
    if (previousDocument.current !== document && status.step !== "idle" && status.step !== "confirming") {
      setStatus({ step: "idle" });
    }
    previousDocument.current = document;
  }, [document, status.step]);

  const publish = () => {
    setStatus({ step: "publishing" });
    publishFormDefinition(competitionId, document)
      .then((response) => {
        setStatus({ step: "published", versionNumber: Number(response.versionNumber) });
        onPublished(Number(response.versionNumber));
      })
      .catch((error: unknown) => {
        if (error instanceof ApiError && error.status === 400) {
          setStatus({
            step: "error",
            message: "Formularz nie przeszedł sprawdzenia. Popraw pola niżej i spróbuj ponownie.",
            fieldErrors: error.fieldErrors,
          });
          return;
        }
        // 409 covers two different reasons on the backend (a race between two
        // publications, or the competition having gone inactive underneath
        // this screen): `detail` is written deliberately for an operator to
        // read, so it is shown as is instead of one guessed generic message.
        if (error instanceof ApiError && error.status === 409) {
          setStatus({
            step: "error",
            message: error.detail ?? "Publikacja się nie powiodła. Odśwież stronę i spróbuj ponownie.",
            fieldErrors: {},
          });
          return;
        }
        setStatus({ step: "error", message: GENERIC_ERROR, fieldErrors: {} });
      });
  };

  if (status.step === "published") {
    return (
      <p className="rounded-sm border border-border-muted bg-surface-muted px-3 py-2 text-sm">
        Opublikowano wersję {status.versionNumber}. Ta wersja trafi do następnego wnioskodawcy;
        wnioski już rozpoczęte zostają przy swojej.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-2 rounded-sm border border-border-muted px-3 py-2">
      {status.step === "idle" ? (
        <button
          type="button"
          className="self-start rounded-sm border border-brand-accent px-4 py-2 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg"
          onClick={() => setStatus({ step: "confirming" })}
        >
          Opublikuj formularz
        </button>
      ) : null}

      {status.step === "confirming" ? (
        <div className="flex flex-col gap-2 text-sm">
          <p>
            Nowa wersja zacznie obowiązywać dla kolejnych wnioskodawców. Wnioski już rozpoczęte
            zostają przy wersji, którą widzieli, i się nie zmienią.
          </p>
          <div className="flex gap-3">
            <button
              type="button"
              className="rounded-sm border border-brand-accent px-4 py-2 text-brand-accent-text hover:bg-brand-accent hover:text-bg"
              onClick={publish}
            >
              Tak, opublikuj
            </button>
            <button type="button" className="underline" onClick={() => setStatus({ step: "idle" })}>
              Anuluj
            </button>
          </div>
        </div>
      ) : null}

      {status.step === "publishing" ? <p className="text-sm">Publikowanie…</p> : null}

      {status.step === "error" ? (
        <div role="alert" className="flex flex-col gap-1 text-sm text-brand-accent-text">
          <p>{status.message}</p>
          {Object.entries(status.fieldErrors).length > 0 ? (
            <ul className="list-inside list-disc">
              {Object.entries(status.fieldErrors).map(([path, messages]) => (
                <li key={path}>
                  {path}: {messages.join(" ")}
                </li>
              ))}
            </ul>
          ) : null}
          <button
            type="button"
            className="self-start underline"
            onClick={() => setStatus({ step: "idle" })}
          >
            Spróbuj ponownie
          </button>
        </div>
      ) : null}
    </div>
  );
}
