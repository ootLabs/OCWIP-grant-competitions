import { EmptyState } from "@/components/empty-state";

import { applicantPanelRoot } from "./navigation";

/**
 * Moje wnioski. The list itself is T-34; what stands here is the state that
 * list shows when it is empty, which is every account's first visit.
 */
export default function ApplicationsPage() {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Moje wnioski</h1>
      <EmptyState
        title="Nie masz jeszcze żadnego wniosku"
        action={{
          // Not a literal path: navigation.ts is the only place this panel
          // spells its own addresses.
          href: `${applicantPanelRoot}/competitions`,
          label: "Zobacz aktualne konkursy",
        }}
      >
        Wniosek zaczyna się od wybrania konkursu, do którego chcesz go złożyć.
        Rozpoczęty wniosek zapisuje się jako roboczy i możesz do niego wracać
        aż do zamknięcia naboru.
      </EmptyState>
    </section>
  );
}
