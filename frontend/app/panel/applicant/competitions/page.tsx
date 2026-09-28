import Link from "next/link";

import { EmptyState } from "@/components/empty-state";
import { statusActionClassName } from "@/components/status-page";
import { competitionPath, fetchPublicCompetitions, type PublicCompetition } from "@/lib/competitions";

// Read at request time: a nabór opens and closes to the minute (T-21).
export const dynamic = "force-dynamic";

/**
 * Aktualne konkursy (T-99): the calls taking applications right now, each
 * leading to its public page, where "Wypełnij wniosek" is.
 *
 * The list itself stays public and single, at /competitions (T-23, decision
 * D6): this panel shows the open ones and points there, rather than keeping
 * a signed in copy that would drift away from it.
 */
export default async function CompetitionsPage() {
  let open: PublicCompetition[] = [];
  let failed = false;
  try {
    open = (await fetchPublicCompetitions()).filter((competition) => competition.intake.acceptsApplications);
  } catch {
    failed = true;
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Aktualne konkursy</h1>

      {failed ? (
        <p className="text-sm">
          Nie udało się pobrać konkursów.{" "}
          <Link className="underline" href="/competitions">
            Zobacz je na stronie publicznej
          </Link>
          .
        </p>
      ) : open.length === 0 ? (
        <EmptyState
          title="Nie ma teraz otwartego naboru"
          action={{ href: "/competitions", label: "Zobacz wszystkie konkursy" }}
        >
          Konkursy ogłasza OCWIP i pojawią się tutaj same, gdy tylko nabór
          ruszy. Zajrzyj za jakiś czas, nie musisz nic w tej sprawie zgłaszać.
        </EmptyState>
      ) : (
        <ul className="divide-y divide-border-muted border-y border-border-muted">
          {open.map((competition) => (
            <li key={competition.id} className="flex items-center justify-between gap-4 py-3">
              <div>
                <p className="text-sm text-text">
                  {competition.number} - {competition.title}
                </p>
                <p className="text-sm">{competition.intake.message}</p>
              </div>
              <Link className={statusActionClassName} href={competitionPath(competition.id)}>
                Zobacz konkurs
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
