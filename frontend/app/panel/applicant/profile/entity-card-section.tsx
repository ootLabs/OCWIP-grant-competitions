"use client";

import { useEffect, useState } from "react";

import { EntityCardForm } from "@/components/entity-card/entity-card-form";
import { EntityCardSummary } from "@/components/entity-card/entity-card-summary";
import { fetchEntityCard, type EntityCardResponse } from "@/lib/entity-card";
import { formatMoment } from "@/lib/format";

import { PendingRequests } from "./pending-requests";

/**
 * One card the person acts for, on "Mój profil": the data, a correction, who
 * else has access (T-93a), and for the founder the requests to join.
 */
export function EntityCardSection({ entityId }: { entityId: string }) {
  const [card, setCard] = useState<EntityCardResponse | null>(null);
  const [failed, setFailed] = useState(false);
  const [editing, setEditing] = useState(false);
  const [saved, setSaved] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    fetchEntityCard(entityId)
      .then((found) => current && setCard(found))
      .catch(() => current && setFailed(true));
    return () => {
      current = false;
    };
  }, [entityId, attempt]);

  if (failed) {
    return (
      <p className="text-sm">
        Nie udało się pobrać danych.{" "}
        <button
          type="button"
          className="underline"
          onClick={() => {
            setFailed(false);
            setAttempt((value) => value + 1);
          }}
        >
          Spróbuj ponownie
        </button>
        .
      </p>
    );
  }

  if (card === null) {
    return <p className="text-sm">Wczytywanie danych…</p>;
  }

  return (
    <section aria-label={card.card.name} className="flex flex-col gap-4 border-t border-border-muted pt-4">
      <h2 className="text-xl">{card.card.name}</h2>

      {editing ? (
        <EntityCardForm
          initial={card.card}
          entityId={card.id}
          submitLabel="Zapisz poprawki"
          onSaved={(updated) => {
            setCard(updated);
            setEditing(false);
            setSaved(true);
          }}
          onCancel={() => setEditing(false)}
        />
      ) : (
        <>
          {saved ? (
            <p role="status" className="text-sm">
              Zapisano. Złożone wnioski zachowują dane z chwili złożenia.
            </p>
          ) : null}
          <p className="text-sm">Dane zaktualizowane {formatMoment(card.updatedAt)}.</p>
          <EntityCardSummary card={card.card} />
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
      )}

      <section aria-label="Osoby z dostępem" className="flex flex-col gap-2">
        <h3 className="text-base">Osoby z dostępem do karty</h3>
        <ul className="list-disc pl-5 text-sm">
          {card.members.map((member) => (
            <li key={`${member.firstName}-${member.lastName}-${member.since}`}>
              {member.firstName} {member.lastName}
              {member.isFounder ? " (osoba, która założyła kartę i zatwierdza prośby o dostęp)" : ""}
            </li>
          ))}
        </ul>
      </section>

      {card.isFounder ? (
        <PendingRequests entityId={card.id} onDecided={() => setAttempt((value) => value + 1)} />
      ) : null}
    </section>
  );
}
