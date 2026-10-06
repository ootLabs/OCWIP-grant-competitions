"use client";

import { useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import { EntityCardForm } from "@/components/entity-card/entity-card-form";
import { EntityCardSummary } from "@/components/entity-card/entity-card-summary";
import { fetchMyEntityCard, type EntityCardResponse } from "@/lib/entity-card";
import { formatMoment } from "@/lib/format";

import { applicantPanelRoot } from "../navigation";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly card: EntityCardResponse | null };

/**
 * Mój profil (T-93): the Podmiot card, read and corrected at any time. A
 * correction changes the card, never an application already submitted,
 * which keeps its own copy.
 */
export default function ProfilePage() {
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [editing, setEditing] = useState(false);
  const [saved, setSaved] = useState(false);
  const [attempt, setAttempt] = useState(0);

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

  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-4">
      <h1 className="text-2xl">Mój profil</h1>

      {load.status === "loading" ? <p className="text-sm">Wczytywanie danych…</p> : null}

      {load.status === "error" ? (
        <p className="text-sm">
          Nie udało się pobrać danych.{" "}
          <button type="button" className="underline" onClick={() => setAttempt((value) => value + 1)}>
            Spróbuj ponownie
          </button>
          .
        </p>
      ) : null}

      {load.status === "ready" && load.card === null ? (
        <EmptyState
          title="Nie mamy jeszcze danych Twojego podmiotu"
          action={{ href: `${applicantPanelRoot}/competitions`, label: "Zobacz aktualne konkursy" }}
        >
          Dane organizacji albo grupy nieformalnej podajesz przy pierwszym wniosku i od tego
          momentu widzisz je w tym miejscu.
        </EmptyState>
      ) : null}

      {load.status === "ready" && load.card !== null && editing ? (
        <EntityCardForm
          initial={load.card.card}
          exists
          submitLabel="Zapisz poprawki"
          onSaved={(card) => {
            setLoad({ status: "ready", card });
            setEditing(false);
            setSaved(true);
          }}
          onCancel={() => setEditing(false)}
        />
      ) : null}

      {load.status === "ready" && load.card !== null && !editing ? (
        <>
          {saved ? (
            <p role="status" className="text-sm">
              Zapisano. Złożone wnioski zachowują dane z chwili złożenia.
            </p>
          ) : null}
          <p className="text-sm">Dane zaktualizowane {formatMoment(load.card.updatedAt)}.</p>
          <EntityCardSummary card={load.card.card} />
          <p>
            <button
              type="button"
              className="text-sm underline"
              onClick={() => {
                setSaved(false);
                setEditing(true);
              }}
            >
              Popraw dane
            </button>
          </p>
        </>
      ) : null}
    </section>
  );
}
