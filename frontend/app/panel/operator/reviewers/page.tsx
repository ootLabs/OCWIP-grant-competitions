import { EmptyState } from "@/components/empty-state";

/**
 * Recenzenci. Managing reviewers is T-37. The empty state says when they are
 * needed, so nobody looks for a missing step before the call even closes.
 */
export default function ReviewersPage() {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Recenzenci</h1>
      <EmptyState title="Nie ma jeszcze żadnego recenzenta">
        Recenzenci oceniają wnioski po zamknięciu naboru. Dodasz ich, zanim
        ocena się zacznie, i przypiszesz im wnioski do oceny.
      </EmptyState>
    </section>
  );
}
