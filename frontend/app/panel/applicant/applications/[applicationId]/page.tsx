"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import { ApiError } from "@/lib/api-client";
import {
  fetchApplication,
  fetchApplicationForm,
  fetchAttachments,
  type Application,
  type ApplicationForm,
  type Attachment,
} from "@/lib/applicant-applications";
import { fetchPublicCompetition, type PublicCompetition } from "@/lib/competitions";

import { applicantPanelRoot } from "../../navigation";
import { DraftWorkspace } from "./draft-workspace";
import { SubmittedView } from "./submitted-view";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "missing" }
  | {
      readonly status: "ready";
      readonly application: Application;
      readonly form: ApplicationForm;
      readonly competition: PublicCompetition;
      readonly attachments: Attachment[];
    };

/**
 * One application, from either side of the line T-33 draws: a draft opens
 * DraftWorkspace, a submitted one opens SubmittedView, decided once here so
 * neither has to guard against being handed the other's state.
 */
export default function ApplicationPage() {
  const { applicationId } = useParams<{ applicationId: string }>();
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    setLoad({ status: "loading" });

    (async () => {
      const application = await fetchApplication(applicationId);
      const [form, competition, attachments] = await Promise.all([
        fetchApplicationForm(applicationId),
        fetchPublicCompetition(application.competitionId),
        fetchAttachments(applicationId),
      ]);

      if (competition === null) {
        // The competition behind an existing application has no public
        // address any more (deactivated). Rare, and nothing this screen can
        // recover from on its own.
        throw new Error("competition unavailable");
      }

      return { application, form, competition, attachments };
    })()
      .then((ready) => {
        if (current) {
          setLoad({ status: "ready", ...ready });
        }
      })
      .catch((error: unknown) => {
        if (!current) {
          return;
        }
        setLoad({
          status:
            error instanceof ApiError && (error.status === 404 || error.status === 403)
              ? "missing"
              : "error",
        });
      });

    return () => {
      current = false;
    };
  }, [applicationId, attempt]);

  // Kept live from DraftWorkspace's own uploads and replacements, not only
  // from the GET this effect ran once at mount: onSubmitted below hands this
  // same value to SubmittedView, and it has to be whatever is actually on
  // the application right now, not the list from before this visit's edits.
  const onAttachmentsChange = useCallback((attachments: Attachment[]) => {
    setLoad((current) => (current.status === "ready" ? { ...current, attachments } : current));
  }, []);

  const back = applicantPanelRoot;

  return (
    <section className="flex max-w-3xl flex-col gap-4">
      <Link href={back} className="text-sm underline">
        Moje wnioski
      </Link>

      {load.status === "loading" ? <p className="text-sm">Wczytywanie wniosku…</p> : null}

      {load.status === "error" ? (
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
      ) : null}

      {load.status === "missing" ? (
        <EmptyState
          title="Nie ma takiego wniosku"
          action={{ href: back, label: "Wróć do listy wniosków" }}
        >
          Ten adres nie prowadzi do żadnego z Twoich wniosków. Mógł zostać
          wpisany z pomyłką albo dotyczy cudzego wniosku.
        </EmptyState>
      ) : null}

      {load.status === "ready" && load.application.status !== "Draft" ? (
        <SubmittedView
          application={load.application}
          form={load.form}
          competitionTitle={load.competition.title}
          attachments={load.attachments}
        />
      ) : null}

      {load.status === "ready" && load.application.status === "Draft" ? (
        <DraftWorkspace
          key={load.application.id}
          application={load.application}
          form={load.form}
          competition={load.competition}
          initialAttachments={load.attachments}
          onAttachmentsChange={onAttachmentsChange}
          onSubmitted={(submitted) =>
            setLoad((current) =>
              current.status === "ready" ? { ...current, application: submitted } : current,
            )
          }
        />
      ) : null}
    </section>
  );
}
