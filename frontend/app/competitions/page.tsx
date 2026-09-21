import type { Metadata } from "next";

import { EmptyState } from "@/components/empty-state";
import { fetchPublicCompetitions } from "@/lib/competitions";

import { CompetitionCard } from "./competition-card";

export const metadata: Metadata = {
  title: "Konkursy OCWIP",
  description:
    "Konkursy dotacyjne ogłaszane przez Opolskie Centrum Wspierania Inicjatyw Pozarządowych. Terminy, kwoty i warunki, bez zakładania konta.",
};

/**
 * Every announced competition, readable without an account (T-23, D6).
 *
 * Rendered on the server, which is what makes it work with no JavaScript, in a
 * link preview and in a search result. The backend already decides what a
 * guest may see: drafts have no public address at all and archived
 * competitions leave this listing while keeping theirs.
 */
export default async function CompetitionsPage() {
  const competitions = await fetchPublicCompetitions();

  return (
    <section className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-3xl">Konkursy</h1>
        <p className="max-w-prose text-sm">
          Wszystkie konkursy dotacyjne OCWIP w jednym miejscu. Nie musisz mieć
          konta, żeby przeczytać warunki; konto jest potrzebne dopiero do
          złożenia wniosku.
        </p>
      </div>

      {competitions.length === 0 ? (
        <EmptyState title="Nie ma teraz ogłoszonego konkursu">
          Konkursy ogłasza OCWIP i pojawią się na tej stronie same, gdy tylko
          nabór ruszy. Zajrzyj za jakiś czas, nie musisz nic w tej sprawie
          zgłaszać.
        </EmptyState>
      ) : (
        <ul className="flex list-none flex-col gap-4">
          {competitions.map((competition) => (
            <CompetitionCard competition={competition} key={competition.id} />
          ))}
        </ul>
      )}
    </section>
  );
}
