import { EmptyState } from "@/components/empty-state";

/**
 * Aktualne konkursy.
 *
 * The list itself is public and lives at /competitions (T-23, decision D6):
 * reading what OCWIP is running never required an account, so there is one
 * list and this panel points at it rather than keeping a signed in copy that
 * would drift away from it.
 *
 * The empty state is not a placeholder: an applicant who signs in between
 * calls for proposals sees exactly this, and "no open call right now" is the
 * true answer, not a missing screen.
 */
export default function CompetitionsPage() {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Aktualne konkursy</h1>
      <EmptyState
        title="Nie ma teraz otwartego naboru"
        action={{ href: "/competitions", label: "Zobacz wszystkie konkursy" }}
      >
        Konkursy ogłasza OCWIP i pojawią się tutaj same, gdy tylko nabór
        ruszy. Zajrzyj za jakiś czas, nie musisz nic w tej sprawie zgłaszać.
      </EmptyState>
    </section>
  );
}
