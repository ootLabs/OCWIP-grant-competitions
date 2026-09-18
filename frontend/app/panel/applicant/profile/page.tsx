import { EmptyState } from "@/components/empty-state";

/**
 * Mój profil. What the profile holds depends on decision R-01
 * (docs/runbook/rozbieznosci.md), so this says where the data will come from
 * and promises nothing about its shape.
 */
export default function ProfilePage() {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Mój profil</h1>
      <EmptyState title="Nie mamy jeszcze danych Twojego podmiotu">
        Dane organizacji albo grupy nieformalnej podajesz przy pierwszym
        wniosku i od tego momentu widzisz je w tym miejscu.
      </EmptyState>
    </section>
  );
}
