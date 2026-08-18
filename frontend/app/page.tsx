import { apiBaseUrl } from "@/lib/api-client";

export default function HomePage() {
  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-6 px-6 py-16">
      <h1 className="text-3xl font-bold text-brand-700">
        Generator konkursów OCWIP
      </h1>

      <p className="text-muted">
        Szkielet aplikacji. Front, API i baza danych stoją i widzą się nawzajem.
        Właściwe ekrany powstają według kart na Trello.
      </p>

      <section className="rounded-lg border border-brand-500/30 bg-brand-50 p-4">
        <h2 className="font-semibold">Sprawdzenie połączenia</h2>
        <ul className="mt-2 list-inside list-disc text-sm">
          <li>
            API:{" "}
            <a className="underline" href={`${apiBaseUrl}/health`}>
              {apiBaseUrl}/health
            </a>
          </li>
          <li>
            API i baza:{" "}
            <a className="underline" href={`${apiBaseUrl}/health/db`}>
              {apiBaseUrl}/health/db
            </a>
          </li>
        </ul>
      </section>
    </main>
  );
}
