import Link from "next/link";

import type { PublicCompetition } from "@/lib/competitions";
import { competitionPath } from "@/lib/competitions";
import { formatAmount } from "@/lib/format";

import { statusLabels } from "./labels";

/**
 * One competition in the public list (T-23).
 *
 * Carries the three things somebody decides on before they open anything:
 * whether the intake is running, how much money is on the table and by when.
 * Everything else waits on the competition page, because a list of fifteen
 * calls each spelling out its cost categories is a list nobody reads.
 *
 * The whole card is not a link: a link wrapping several lines of text reads as
 * one long unlabelled link to a screen reader. The title is the link, and it
 * names the competition, which is what a reader jumping between links needs.
 */
export function CompetitionCard({
  competition,
}: {
  competition: PublicCompetition;
}) {
  const { intake } = competition;

  return (
    <li className="rounded-sm border border-border px-4 py-4 sm:px-5">
      <p className="text-sm">
        Nr {competition.number} · {statusLabels[competition.status]}
      </p>

      <h2 className="mt-1 text-xl">
        <Link
          className="text-text-link underline"
          href={competitionPath(competition.id)}
        >
          {competition.title}
        </Link>
      </h2>

      <p className="mt-2 text-sm">{intake.message}</p>

      {/* The deadline is inside the sentence above, written by the rule that
          owns it, so the card adds the one figure the sentence has no room
          for rather than repeating the date in its own words. */}
      <dl className="mt-3 text-sm">
        <dt className="inline font-semibold">Maksymalna dotacja: </dt>
        <dd className="inline">{formatAmount(competition.maxGrantAmount)}</dd>
      </dl>
    </li>
  );
}
