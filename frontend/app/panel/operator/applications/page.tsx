import { EmptyState } from "@/components/empty-state";

import { operatorPanelRoot } from "../navigation";

/**
 * Wnioski. The list and its statuses are T-35. Empty here has one cause worth
 * naming: nothing can arrive before a call for proposals is open.
 */
export default function ApplicationsPage() {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Wnioski</h1>
      <EmptyState
        title="Nie ma jeszcze żadnego wniosku"
        action={{ href: operatorPanelRoot, label: "Przejdź do konkursów" }}
      >
        Wnioski trafiają tutaj z otwartych naborów, ze wszystkich konkursów
        naraz. Dopóki żaden nabór nie trwa, nie ma czego składać.
      </EmptyState>
    </section>
  );
}
