"use client";

import { useEffect, useState } from "react";

import { decideAsFounder, fetchPendingAccessRequests, type PendingAccessRequest } from "@/lib/access-requests";
import { apiErrorMessage } from "@/lib/api-client";
import { formatMoment } from "@/lib/format";

/**
 * Requests to join a card, for its founder (T-93a, report decision 7). The
 * address is shown because the founder lets in only somebody they recognise:
 * a member sees every application of the organisation, drafts included.
 */
export function PendingRequests({ entityId, onDecided }: { entityId: string; onDecided: () => void }) {
  const [requests, setRequests] = useState<PendingAccessRequest[] | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    fetchPendingAccessRequests(entityId)
      .then((found) => current && setRequests(found))
      .catch(() => current && setFailure("Nie udało się pobrać próśb o dostęp."));
    return () => {
      current = false;
    };
  }, [entityId, attempt]);

  async function decide(request: PendingAccessRequest, approve: boolean) {
    setBusy(request.id);
    setFailure(null);

    try {
      await decideAsFounder(entityId, request.id, approve);
      setAttempt((value) => value + 1);
      onDecided();
    } catch (error) {
      setFailure(apiErrorMessage(error, "Nie udało się zapisać decyzji. Spróbuj ponownie."));
    } finally {
      setBusy(null);
    }
  }

  if (requests === null && failure === null) {
    return null;
  }

  return (
    <section aria-label="Prośby o dostęp" className="flex flex-col gap-2">
      <h3 className="text-base">Prośby o dostęp</h3>
      {failure !== null ? (
        <p role="alert" className="text-sm text-brand-accent-text">
          {failure}
        </p>
      ) : null}
      {requests !== null && requests.length === 0 ? <p className="text-sm">Nikt nie czeka na dostęp.</p> : null}
      {requests !== null && requests.length > 0 ? (
        <>
          <p className="text-sm">
            Osoba z dostępem widzi wszystkie wnioski organizacji, także robocze, i może je składać.
            Zatwierdzaj tylko osoby, które znasz.
          </p>
          <ul className="flex flex-col gap-3">
            {requests.map((request) => (
              <li key={request.id} className="flex flex-col gap-2 border border-border-muted p-3 text-sm sm:flex-row sm:items-center sm:justify-between">
                <span>
                  {request.firstName} {request.lastName}, {request.email}
                  <span className="block">Prośba z {formatMoment(request.requestedAt)}</span>
                </span>
                <span className="flex gap-3">
                  <button
                    type="button"
                    className="underline"
                    disabled={busy !== null}
                    onClick={() => void decide(request, true)}
                  >
                    Zatwierdź
                  </button>
                  <button
                    type="button"
                    className="underline"
                    disabled={busy !== null}
                    onClick={() => void decide(request, false)}
                  >
                    Odrzuć
                  </button>
                </span>
              </li>
            ))}
          </ul>
        </>
      ) : null}
    </section>
  );
}
