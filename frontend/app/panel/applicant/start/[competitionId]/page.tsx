"use client";

import { useParams, useRouter } from "next/navigation";
import { useEffect, useState } from "react";

import { EntityCardForm } from "@/components/entity-card/entity-card-form";
import { EntityCardSummary } from "@/components/entity-card/entity-card-summary";
import { accountSubmitClassName } from "@/components/account-field";
import { apiErrorMessage } from "@/lib/api-client";
import { createDraft } from "@/lib/applicant-applications";
import { fetchPublicCompetition, type PublicCompetition } from "@/lib/competitions";
import {
  emptyCard,
  fetchEntityCard,
  fetchMyEntities,
  type EntityCardResponse,
  type EntityCardSummary as CardListItem,
} from "@/lib/entity-card";
import { formatMoment } from "@/lib/format";

import { applicantPanelRoot } from "../../navigation";
import { EntityChoice } from "./entity-choice";
import { WhatToPrepare } from "./what-to-prepare";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly cards: readonly CardListItem[] };

/**
 * The first step of every application (T-93, pola.md part I): the data of
 * whoever applies. At the first application an empty card, which becomes the
 * Podmiot's card once saved; at every one after that the card filled in, with
 * the date it was last updated and two ways on: "dane są aktualne" or
 * "popraw", a correction going to the card itself.
 *
 * Somebody who acts for several cards first says for which one (T-93a), and
 * anybody may add another: a NIP that is taken leads to a request to join.
 *
 * Only after this does a draft exist, so no draft ever belongs to nobody.
 */
export default function StartApplicationPage() {
  const { competitionId } = useParams<{ competitionId: string }>();
  const router = useRouter();
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [chosen, setChosen] = useState<string | null>(null);
  const [card, setCard] = useState<EntityCardResponse | null>(null);
  const [adding, setAdding] = useState(false);
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

    fetchMyEntities()
      .then((cards) => {
        if (!current) {
          return;
        }
        setLoad({ status: "ready", cards });
        // One card: nothing to choose.
        setChosen(cards.length === 1 ? cards[0].id : null);
      })
      .catch(() => current && setLoad({ status: "error" }));

    return () => {
      current = false;
    };
  }, [attempt]);

  useEffect(() => {
    let current = true;
    setCard(null);

    if (chosen !== null) {
      fetchEntityCard(chosen)
        .then((found) => current && setCard(found))
        .catch(() => current && setLoad({ status: "error" }));
    }

    return () => {
      current = false;
    };
  }, [chosen]);

  async function startDraft(entityId: string) {
    setStarting(true);
    setFailure(null);

    try {
      const draft = await createDraft(competitionId, entityId);
      router.replace(`${applicantPanelRoot}/applications/${draft.id}`);
    } catch (error) {
      setFailure(apiErrorMessage(error, "Nie udało się rozpocząć wniosku. Spróbuj ponownie."));
      setStarting(false);
    }
  }

  const cards = load.status === "ready" ? load.cards : [];
  const founding = load.status === "ready" && (cards.length === 0 || adding);

  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-4">
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

      {founding ? (
        <>
          <p className="text-sm">
            Podajesz je raz. Po zapisaniu staną się kartą Twojej organizacji albo grupy i przy
            kolejnych wnioskach zobaczysz je wypełnione.
          </p>
          <EntityCardForm
            initial={emptyCard()}
            entityId={null}
            submitLabel="Zapisz dane i przejdź do wniosku"
            onSaved={(saved) => {
              // The card exists from here on, even if starting the draft
              // fails: a second try must not POST it again.
              setAdding(false);
              setLoad({ status: "ready", cards: [...cards, summaryOf(saved)] });
              setChosen(saved.id);
              void startDraft(saved.id);
            }}
            onCancel={cards.length > 0 ? () => setAdding(false) : undefined}
          />
        </>
      ) : null}

      {!founding && cards.length > 1 ? <EntityChoice cards={cards} chosen={chosen} onChoose={setChosen} /> : null}

      {!founding && card !== null && editing ? (
        <EntityCardForm
          initial={card.card}
          entityId={card.id}
          submitLabel="Zapisz poprawki i przejdź do wniosku"
          onSaved={(saved) => {
            // Back to the summary, whose button stays disabled while the
            // draft starts: the form's own button would be free again the
            // moment the PUT answers, and a second click a second draft.
            setCard(saved);
            setEditing(false);
            void startDraft(saved.id);
          }}
          onCancel={() => setEditing(false)}
        />
      ) : null}

      {!founding && card !== null && !editing ? (
        <>
          <p className="text-sm">
            Dane zaktualizowane {formatMoment(card.updatedAt)}. Sprawdź, czy są aktualne. Poprawka
            zmienia kartę, a nie wnioski już złożone.
          </p>
          <EntityCardSummary card={card.card} />
          <div className="flex flex-col gap-3 sm:flex-row">
            <button
              type="button"
              className={accountSubmitClassName}
              disabled={starting}
              onClick={() => void startDraft(card.id)}
            >
              {starting ? "Rozpoczynanie…" : "Dane są aktualne"}
            </button>
            <button type="button" className="text-sm underline" disabled={starting} onClick={() => setEditing(true)}>
              Popraw
            </button>
          </div>
        </>
      ) : null}

      {!founding && cards.length > 0 ? (
        <p className="text-sm">
          Składasz wniosek w imieniu innej organizacji albo grupy?{" "}
          <button type="button" className="underline" disabled={starting} onClick={() => setAdding(true)}>
            Dodaj podmiot
          </button>
          .
        </p>
      ) : null}
    </section>
  );
}

function summaryOf(saved: EntityCardResponse): CardListItem {
  return {
    id: saved.id,
    type: saved.card.type,
    name: saved.card.name,
    isFounder: saved.isFounder,
    updatedAt: saved.updatedAt,
  };
}
