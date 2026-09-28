"use client";

import { useParams, useRouter } from "next/navigation";
import { useEffect, useState } from "react";

import { EntityCardForm } from "@/components/entity-card/entity-card-form";
import { EntityCardSummary } from "@/components/entity-card/entity-card-summary";
import { accountSubmitClassName } from "@/components/account-field";
import { apiErrorMessage } from "@/lib/api-client";
import { createDraft } from "@/lib/applicant-applications";
import { fetchPublicCompetition, type PublicCompetition } from "@/lib/competitions";
import { emptyCard, fetchMyEntityCard, type EntityCardResponse } from "@/lib/entity-card";
import { formatMoment } from "@/lib/format";

import { applicantPanelRoot } from "../../navigation";
import { WhatToPrepare } from "./what-to-prepare";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly card: EntityCardResponse | null };

/**
 * The first step of every application (T-93, pola.md part I): the data of
 * whoever applies. At the first application an empty card, which becomes the
 * Podmiot's card once saved; at every one after that the card filled in, with
 * the date it was last updated and two ways on: "dane są aktualne" or
 * "popraw", a correction going to the card itself.
 *
 * Only after this does a draft exist, so no draft ever belongs to nobody.
 */
export default function StartApplicationPage() {
  const { competitionId } = useParams<{ competitionId: string }>();
  const router = useRouter();
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [editing, setEditing] = useState(false);
  const [starting, setStarting] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [competition, setCompetition] = useState<PublicCompetition | null>(null);

  useEffect(() => {
    let current = true;
    // "Co przygotować" is a help, not a condition: without it the card
    // still opens.
    fetchPublicCompetition(competitionId)
      .then((found) => current && setCompetition(found))
      .catch(() => undefined);
    return () => {
      current = false;
    };
  }, [competitionId]);

  useEffect(() => {
    let current = true;
    setLoad({ status: "loading" });

    fetchMyEntityCard()
      .then((card) => current && setLoad({ status: "ready", card }))
      .catch(() => current && setLoad({ status: "error" }));

    return () => {
      current = false;
    };
  }, [attempt]);

  async function startDraft() {
    setStarting(true);
    setFailure(null);

    try {
      const draft = await createDraft(competitionId);
      router.replace(`${applicantPanelRoot}/applications/${draft.id}`);
    } catch (error) {
      setFailure(apiErrorMessage(error, "Nie udało się rozpocząć wniosku. Spróbuj ponownie."));
      setStarting(false);
    }
  }

  return (
    <section className="flex max-w-3xl flex-col gap-4">
      <h1 className="text-2xl">Nowy wniosek{competition ? `: ${competition.title}` : ""}</h1>

      {competition ? <WhatToPrepare competition={competition} /> : null}

      <h2 className="text-xl">Dane wnioskodawcy</h2>

      {load.status === "loading" ? <p className="text-sm">Wczytywanie danych…</p> : null}

      {load.status === "error" ? (
        <p className="text-sm">
          Nie udało się pobrać danych wnioskodawcy.{" "}
          <button type="button" className="underline" onClick={() => setAttempt((value) => value + 1)}>
            Spróbuj ponownie
          </button>
          .
        </p>
      ) : null}

      {failure !== null ? (
        <p role="alert" className="text-sm text-brand-accent-text">
          {failure}
        </p>
      ) : null}

      {load.status === "ready" && load.card === null ? (
        <>
          <p className="text-sm">
            Podajesz je raz. Po zapisaniu staną się kartą Twojej organizacji albo grupy i przy
            kolejnych wnioskach zobaczysz je wypełnione.
          </p>
          <EntityCardForm
            initial={emptyCard()}
            exists={false}
            submitLabel="Zapisz dane i przejdź do wniosku"
            onSaved={(saved) => {
              // The card exists from here on, even if starting the draft
              // fails: a second try must not POST it again.
              setLoad({ status: "ready", card: saved });
              void startDraft();
            }}
          />
        </>
      ) : null}

      {load.status === "ready" && load.card !== null && editing ? (
        <EntityCardForm
          initial={load.card.card}
          exists
          submitLabel="Zapisz poprawki i przejdź do wniosku"
          onSaved={(saved) => {
            // Back to the summary, whose button stays disabled while the
            // draft starts: the form's own button would be free again the
            // moment the PUT answers, and a second click a second draft.
            setLoad({ status: "ready", card: saved });
            setEditing(false);
            void startDraft();
          }}
          onCancel={() => setEditing(false)}
        />
      ) : null}

      {load.status === "ready" && load.card !== null && !editing ? (
        <>
          <p className="text-sm">
            Dane zaktualizowane {formatMoment(load.card.updatedAt)}. Sprawdź, czy są aktualne.
            Poprawka zmienia kartę, a nie wnioski już złożone.
          </p>
          <EntityCardSummary card={load.card.card} />
          <div className="flex flex-col gap-3 sm:flex-row">
            <button
              type="button"
              className={accountSubmitClassName}
              disabled={starting}
              onClick={() => void startDraft()}
            >
              {starting ? "Rozpoczynanie…" : "Dane są aktualne"}
            </button>
            <button
              type="button"
              className="text-sm underline"
              disabled={starting}
              onClick={() => setEditing(true)}
            >
              Popraw
            </button>
          </div>
        </>
      ) : null}
    </section>
  );
}
