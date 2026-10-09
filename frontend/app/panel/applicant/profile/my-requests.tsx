"use client";

import { accessRequestStatusLabels, type MyAccessRequest } from "@/lib/access-requests";
import { formatMoment } from "@/lib/format";

/** The person's own requests to join a card and where each one stands (T-93a). */
export function MyRequests({ requests }: { requests: readonly MyAccessRequest[] }) {
  if (requests.length === 0) {
    return null;
  }

  return (
    <section aria-label="Twoje prośby o dostęp" className="flex flex-col gap-2 border-t border-border-muted pt-4">
      <h2 className="text-xl">Twoje prośby o dostęp</h2>
      <ul className="list-disc pl-5 text-sm">
        {requests.map((request) => (
          <li key={request.id}>
            {request.entityName}: {accessRequestStatusLabels[request.status]}, prośba z{" "}
            {formatMoment(request.requestedAt)}
          </li>
        ))}
      </ul>
      {requests.some((request) => request.status === "Pending") ? (
        <p className="text-sm">
          Prośbę rozpatruje osoba, która założyła kartę. Jeśli nie odpowie przez 7 dni, rozpatrzy ją
          operator OCWIP.
        </p>
      ) : null}
    </section>
  );
}
