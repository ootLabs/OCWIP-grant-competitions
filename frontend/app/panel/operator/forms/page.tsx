import { EmptyState } from "@/components/empty-state";

/**
 * Formularze. The form builder is T-26. Empty state only, because a competition
 * cannot be announced without a form and this is where that starts.
 */
export default function FormsPage() {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Formularze</h1>
      <EmptyState title="Nie ma jeszcze żadnego formularza">
        Formularz wniosku budujesz raz, a potem podpinasz go do konkursu przy
        ogłoszeniu. Ten sam formularz może obsłużyć kilka naborów.
      </EmptyState>
    </section>
  );
}
