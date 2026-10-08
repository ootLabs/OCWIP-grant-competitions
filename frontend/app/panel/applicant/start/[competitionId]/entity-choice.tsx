"use client";

import { entityTypeLabels, type EntityCardSummary } from "@/lib/entity-card";

/**
 * "W imieniu którego podmiotu składasz wniosek?" (T-93a, report step 2.2):
 * asked only of somebody who acts for more than one card.
 */
export function EntityChoice({
  cards,
  chosen,
  onChoose,
}: {
  cards: readonly EntityCardSummary[];
  chosen: string | null;
  onChoose: (id: string) => void;
}) {
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-base">W imieniu którego podmiotu składasz wniosek?</legend>
      {cards.map((card) => (
        <label key={card.id} className="flex items-start gap-2 text-sm">
          <input
            type="radio"
            name="entity"
            className="mt-1"
            checked={chosen === card.id}
            onChange={() => onChoose(card.id)}
          />
          <span>
            {card.name}
            <span className="block">{entityTypeLabels[card.type]}</span>
          </span>
        </label>
      ))}
    </fieldset>
  );
}
