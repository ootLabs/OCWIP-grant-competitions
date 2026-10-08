"use client";

import { useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import { EntityCardForm } from "@/components/entity-card/entity-card-form";
import { fetchMyAccessRequests, type MyAccessRequest } from "@/lib/access-requests";
import { emptyCard, fetchMyEntities, type EntityCardSummary } from "@/lib/entity-card";

import { applicantPanelRoot } from "../navigation";
import { EntityCardSection } from "./entity-card-section";
import { MyRequests } from "./my-requests";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | {
      readonly status: "ready";
      readonly cards: readonly EntityCardSummary[];
      readonly requests: readonly MyAccessRequest[];
    };

/**
 * Mój profil (T-93, T-93a): every card the person acts for, read and
 * corrected at any time, with who else has access, the founder's requests to
 * decide and the person's own requests to join. A correction changes the
 * card, never an application already submitted, which keeps its own copy.
 */
export default function ProfilePage() {
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [adding, setAdding] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    setLoad({ status: "loading" });

    Promise.all([fetchMyEntities(), fetchMyAccessRequests()])
      .then(([cards, requests]) => current && setLoad({ status: "ready", cards, requests }))
      .catch(() => current && setLoad({ status: "error" }));

    return () => {
      current = false;
    };
  }, [attempt]);

  const reload = () => setAttempt((value) => value + 1);

  // After a request the form stays as it is, showing "wysłana"; only the
  // list of requests below catches up.
  const refreshRequests = () => {
    fetchMyAccessRequests()
      .then((requests) => setLoad((current) => (current.status === "ready" ? { ...current, requests } : current)))
      .catch(() => undefined);
  };

  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-4">
      <h1 className="text-2xl">Mój profil</h1>

      {load.status === "loading" ? <p className="text-sm">Wczytywanie danych…</p> : null}

      {load.status === "error" ? (
        <p className="text-sm">
          Nie udało się pobrać danych.{" "}
          <button type="button" className="underline" onClick={reload}>
            Spróbuj ponownie
          </button>
          .
        </p>
      ) : null}

      {load.status === "ready" && load.cards.length === 0 && !adding ? (
        <EmptyState
          title="Nie mamy jeszcze danych Twojego podmiotu"
          action={{ href: `${applicantPanelRoot}/competitions`, label: "Zobacz aktualne konkursy" }}
        >
          Dane organizacji albo grupy nieformalnej podajesz przy pierwszym wniosku i od tego
          momentu widzisz je w tym miejscu. Jeśli Twoja organizacja ma już kartę w systemie, możesz
          poprosić o dostęp do niej.
        </EmptyState>
      ) : null}

      {load.status === "ready"
        ? load.cards.map((card) => <EntityCardSection key={card.id} entityId={card.id} />)
        : null}

      {load.status === "ready" ? <MyRequests requests={load.requests} /> : null}

      {load.status === "ready" && adding ? (
        <section aria-label="Nowy podmiot" className="flex flex-col gap-4 border-t border-border-muted pt-4">
          <h2 className="text-xl">Nowy podmiot</h2>
          <p className="text-sm">
            Jeśli organizacja ma już kartę w systemie, po wpisaniu jej NIP-u poprosisz o dostęp do
            niej, zamiast zakładać drugą.
          </p>
          <EntityCardForm
            initial={emptyCard()}
            entityId={null}
            submitLabel="Zapisz dane"
            onSaved={() => {
              setAdding(false);
              reload();
            }}
            onCancel={() => setAdding(false)}
            onAccessRequested={refreshRequests}
          />
        </section>
      ) : null}

      {load.status === "ready" && !adding ? (
        <p>
          <button type="button" className="text-sm underline" onClick={() => setAdding(true)}>
            Dodaj organizację albo grupę
          </button>
        </p>
      ) : null}
    </section>
  );
}
