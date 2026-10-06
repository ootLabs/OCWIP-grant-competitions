import Link from "next/link";

import { StatusBadge } from "@/components/ui/status-badge";
import { cardClassName } from "@/components/ui/styles";
import type { PublicCompetition } from "@/lib/competitions";
import { competitionPath } from "@/lib/competitions";
import { formatAmount } from "@/lib/format";

import { statusLabels, statusTones } from "./labels";

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
  headingLevel = 2,
}: {
  competition: PublicCompetition;
  /** 3 where the list sits under a section heading of its own (the home page, T-99). */
  headingLevel?: 2 | 3;
}) {
  const { intake } = competition;
  const Heading = headingLevel === 3 ? "h3" : "h2";

  return (
    <li className={`${cardClassName} flex flex-col gap-4 p-6`}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <StatusBadge tone={statusTones[competition.status]}>{statusLabels[competition.status]}</StatusBadge>
        <span className="text-sm text-text-muted">Nr {competition.number}</span>
      </div>

      <Heading className="text-2xl leading-tight">
        <Link className="text-text underline decoration-2 underline-offset-4 hover:text-brand-accent-text" href={competitionPath(competition.id)}>
          {competition.title}
        </Link>
      </Heading>

      {/* The deadline is inside this sentence, written by the rule that owns
          it, so the card adds the figures the sentence has no room for rather
          than repeating the date in its own words. */}
      <p className="text-sm">{intake.message}</p>

      <dl className="mt-auto grid grid-cols-2 gap-3 border-t border-border-muted pt-4 text-sm">
        <div>
          <dt className="text-xs text-text-muted">Maksymalna dotacja</dt>
          <dd className="font-semibold">{formatAmount(competition.maxGrantAmount)}</dd>
        </div>
        {competition.totalPoolAmount == null ? null : (
          <div>
            <dt className="text-xs text-text-muted">Pula konkursu</dt>
            <dd className="font-semibold">{formatAmount(competition.totalPoolAmount)}</dd>
          </div>
        )}
      </dl>
    </li>
  );
}
