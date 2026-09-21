import Link from "next/link";

import type { CompetitionIntake } from "@/lib/competitions";
import { competitionPath } from "@/lib/competitions";
import { loginPath } from "@/lib/session";

/**
 * "Wypełnij wniosek", and the reason it sometimes does nothing (T-23).
 *
 * Whether it leads anywhere is `intake.acceptsApplications` and nothing else.
 * An announced competition is visible from the moment it is published, but the
 * intake opens later and closes to the minute, so this button and the page it
 * sits on are two different moments in the life of the same competition.
 *
 * Closed renders as text, not as a disabled button. A disabled control tells
 * somebody there is something to press and refuses to say why. The why is the
 * sentence the backend wrote, printed once by IntakeCountdown directly above
 * this, so it is not repeated here.
 */
export function ApplyLink({
  competitionId,
  intake,
}: {
  competitionId: string;
  intake: CompetitionIntake;
}) {
  if (!intake.acceptsApplications) {
    return (
      <p className="text-sm">Wniosku nie da się teraz rozpocząć.</p>
    );
  }

  // Straight back to this competition after signing in, because somebody who
  // came from a link in a post has no other way of finding it again.
  const returnUrl = encodeURIComponent(competitionPath(competitionId));

  return (
    <div className="flex flex-col gap-2">
      <Link
        className="inline-flex w-full items-center justify-center rounded-sm bg-brand-accent px-5 py-3 text-bg hover:bg-brand-accent-hover sm:w-auto"
        href={`${loginPath}?returnUrl=${returnUrl}`}
      >
        Wypełnij wniosek
      </Link>
      <p className="text-sm">
        Wniosek składa się po zalogowaniu. Jeśli nie masz jeszcze konta,
        załóż je na tej samej stronie, a po zalogowaniu wrócisz tutaj.
      </p>
    </div>
  );
}
