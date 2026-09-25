"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";

import { createDraft } from "@/lib/applicant-applications";
import type { CompetitionIntake } from "@/lib/competitions";
import { competitionPath } from "@/lib/competitions";
import { fetchCurrentUser, loginPath } from "@/lib/session";

/**
 * "Wypełnij wniosek", and the reason it sometimes does nothing (T-23), and
 * the reason it sometimes starts a draft right away instead of asking to
 * sign in again (T-34, proces.md krok 3.1).
 *
 * Whether it leads anywhere at all is `intake.acceptsApplications` and
 * nothing else. An announced competition is visible from the moment it is
 * published, but the intake opens later and closes to the minute, so this
 * button and the page it sits on are two different moments in the life of
 * the same competition.
 *
 * Closed renders as text, not as a disabled button. A disabled control tells
 * somebody there is something to press and refuses to say why. The why is the
 * sentence the backend wrote, printed once by IntakeCountdown directly above
 * this, so it is not repeated here.
 *
 * The session check is asked here, once, rather than assumed from the page
 * around it: this page is public and anonymous by design (D6), so most of
 * the time nobody is signed in at all, and the one honest answer to "is
 * somebody signed in as an applicant" is GET /me (lib/session.ts).
 */
export function ApplyLink({
  competitionId,
  intake,
}: {
  competitionId: string;
  intake: CompetitionIntake;
}) {
  const router = useRouter();
  const [isApplicant, setIsApplicant] = useState(false);
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let current = true;

    fetchCurrentUser()
      .then((user) => {
        if (current && user?.role === "Applicant") {
          setIsApplicant(true);
        }
      })
      .catch(() => {
        // A backend blip here is treated the same as nobody being signed in
        // yet: the login link below still works, and asks GET /me again the
        // next time this page loads.
      });

    return () => {
      current = false;
    };
  }, []);

  if (!intake.acceptsApplications) {
    return <p className="text-sm">Wniosku nie da się teraz rozpocząć.</p>;
  }

  if (isApplicant) {
    return (
      <div className="flex flex-col gap-2">
        <button
          type="button"
          className="inline-flex w-full items-center justify-center rounded-sm bg-brand-accent px-5 py-3 text-bg hover:bg-brand-accent-hover disabled:opacity-60 sm:w-auto"
          disabled={starting}
          onClick={async () => {
            setStarting(true);
            setError(null);

            try {
              const draft = await createDraft(competitionId);
              router.push(`/panel/applicant/applications/${draft.id}`);
            } catch {
              setError("Nie udało się rozpocząć wniosku. Spróbuj ponownie.");
              setStarting(false);
            }
          }}
        >
          {starting ? "Rozpoczynanie…" : "Wypełnij wniosek"}
        </button>
        {error !== null ? (
          <p role="alert" className="text-sm text-brand-accent-text">
            {error}
          </p>
        ) : null}
      </div>
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
