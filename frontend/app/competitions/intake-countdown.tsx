"use client";

import { useEffect, useState } from "react";

import { formatRemaining, remainingUntil, type Remaining } from "@/lib/countdown";

/**
 * The state of the intake, and how long is left of it (T-23).
 *
 * The sentence comes from the backend rule (T-21) and is printed as it
 * arrives: it already carries the closing date, the hour and the time zone,
 * because D12 says a message names the value that decided it. Rebuilding that
 * sentence here would give the page two answers to one question, and the
 * hours in them would drift apart the first time either side changed.
 *
 * The remaining span is the only part that needs a clock, so it appears after
 * mounting and nowhere else: rendering it on the server too would mean the
 * page and its hydration disagree by exactly one minute, every time a request
 * lands near a minute boundary. Somebody with no JavaScript still gets the
 * sentence, which is the part that carries the deadline.
 *
 * Refreshed every thirty seconds rather than every second. The cut off is to
 * the minute (D7), so a second hand adds no information, and a screen reader
 * pointed at a region that changes every second is unusable.
 */
export function IntakeCountdown({
  closesAt,
  message,
}: {
  closesAt: string | null;
  message: string;
}) {
  // undefined means "not measured yet", which is a different thing from null,
  // "measured, and there is nothing left to count".
  const [remaining, setRemaining] = useState<Remaining | null | undefined>(
    undefined,
  );

  useEffect(() => {
    if (closesAt === null) {
      // Cleared, not left alone: this component stays mounted while the page
      // moves from a competition with a deadline to one without (a client
      // navigation between two competitions reuses it), and a span measured
      // against the old deadline would sit above "nabór ciągły".
      setRemaining(null);

      return;
    }

    const measure = () => setRemaining(remainingUntil(closesAt, new Date()));

    measure();
    const timer = setInterval(measure, 30_000);

    return () => clearInterval(timer);
  }, [closesAt]);

  return (
    <div className="flex flex-col gap-2">
      {remaining ? (
        <p className="flex flex-col">
          <span className="text-sm text-text-muted">Do zamknięcia naboru pozostało</span>{" "}
          <strong className="font-heading text-3xl font-extrabold leading-tight lining-nums">{formatRemaining(remaining)}</strong>
        </p>
      ) : null}
      <p className="text-sm">{message}</p>
    </div>
  );
}
