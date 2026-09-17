/**
 * Konkursy. Empty on purpose: T-15.3 builds the frame, creating and listing
 * competitions is T-22 and the empty state with its next step is T-15.4.
 */
export default function CompetitionsPage() {
  return (
    <section className="flex flex-col gap-3">
      <h1 className="text-2xl">Konkursy</h1>
      <p className="text-sm">Ten ekran powstaje. Wkrótce znajdziesz tu wszystkie konkursy.</p>
    </section>
  );
}
