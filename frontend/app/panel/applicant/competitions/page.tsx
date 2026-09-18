import { EmptyState } from "@/components/empty-state";

/**
 * Aktualne konkursy. The list is T-23. The empty state is not a placeholder
 * for it: an applicant who signs in between calls for proposals sees exactly
 * this, and "no open call right now" is the true answer, not a missing screen.
 */
export default function CompetitionsPage() {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Aktualne konkursy</h1>
      <EmptyState title="Nie ma teraz otwartego naboru">
        Konkursy ogłasza OCWIP i pojawią się tutaj same, gdy tylko nabór
        ruszy. Zajrzyj za jakiś czas, nie musisz nic w tej sprawie zgłaszać.
      </EmptyState>
    </section>
  );
}
