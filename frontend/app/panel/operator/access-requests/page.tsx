"use client";

import { useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import { fetchEscalatedAccessRequests, type EscalatedAccessRequest } from "@/lib/access-requests";

import { EscalatedRequest } from "./escalated-request";

/**
 * Requests to join a Podmiot card that nobody answered for seven days
 * (T-93a, report step 2.2). The founder decides first; the operator steps in
 * here, after checking the person outside the system, and says how.
 */
export default function AccessRequestsPage() {
  const [requests, setRequests] = useState<EscalatedAccessRequest[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    fetchEscalatedAccessRequests()
      .then((found) => current && setRequests(found))
      .catch(() => current && setFailed(true));
    return () => {
      current = false;
    };
  }, [attempt]);

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Prośby o dostęp</h1>
      <p className="text-sm">
        Prośby o dostęp do karty organizacji, których osoba zakładająca kartę nie rozpatrzyła przez
        7 dni. Przed decyzją sprawdź osobę proszącą poza systemem, na przykład telefonicznie albo w
        odpisie z rejestru. Osoba z dostępem widzi wszystkie wnioski organizacji, także robocze.
      </p>

      {failed ? <p className="text-sm">Nie udało się pobrać próśb o dostęp.</p> : null}
      {requests === null && !failed ? <p className="text-sm">Wczytywanie próśb…</p> : null}

      {requests !== null && requests.length === 0 ? (
        <EmptyState title="Nie ma próśb czekających dłużej niż 7 dni">
          Prośbę rozpatruje najpierw osoba, która założyła kartę. Tutaj trafia dopiero wtedy, gdy
          nie odpowie przez tydzień.
        </EmptyState>
      ) : null}

      {requests !== null && requests.length > 0 ? (
        <ul className="flex flex-col gap-4">
          {requests.map((request) => (
            <EscalatedRequest key={request.id} request={request} onDecided={() => setAttempt((value) => value + 1)} />
          ))}
        </ul>
      ) : null}
    </section>
  );
}
