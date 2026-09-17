import { EmptyState } from "@/components/empty-state";

/**
 * Konkursy. Announcing and listing competitions is T-22. This is what the
 * client sees on the very first day, on a system that holds nothing at all.
 */
export default function CompetitionsPage() {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Konkursy</h1>
      <EmptyState title="Nie ma jeszcze żadnego konkursu">
        Konkurs zaczyna się od ogłoszenia: tytuł, terminy naboru, budżet i
        formularz wniosku, który wnioskodawcy wypełnią.
      </EmptyState>
    </section>
  );
}
