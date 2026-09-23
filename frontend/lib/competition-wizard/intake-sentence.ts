/**
 * "Ekran mówi wprost, co oznacza wpisany termin zamknięcia" (T-22, karta
 * 3S8truZC, sekcja "Terminy w UI"): a plain Polish sentence under the end
 * date field in step 1.1, built straight from what the operator just typed.
 *
 * Deliberately NOT the T-21 intake rule (CompetitionIntakeMessage): that
 * rule answers "does this competition accept applications right now" for a
 * competition that already exists, and this screen is about a competition
 * that may not exist yet. Duplicating that rule here would be the second
 * copy AGENTS.md warns about; this is a different, narrower question -
 * "what does the date I just typed mean" - answered from the typed value
 * alone, with no notion of the current moment.
 */
const dateTimeFormatter = new Intl.DateTimeFormat("pl-PL", {
  // UTC, not POLISH_TIME_ZONE: nothing here converts a time zone, it only
  // prints the digits the operator typed back out in a friendlier shape, and
  // pinning to UTC is what keeps that printing independent of the machine
  // this code happens to run on.
  timeZone: "UTC",
  day: "numeric",
  month: "long",
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
});

/**
 * `localValue` is the raw `<input type="datetime-local">` string, read as
 * Polish wall clock time (the same reading to-request.ts sends to the
 * backend). Formatted by echoing the same digits back, never through a
 * conversion, which is what keeps this sentence showing exactly the hour the
 * operator typed.
 */
function formatLocalReading(localValue: string): string {
  const [datePart, timePart = "00:00"] = localValue.split("T");
  const [year, month, day] = datePart.split("-").map(Number);
  const [hour, minute] = timePart.split(":").map(Number);

  return dateTimeFormatter.format(
    new Date(Date.UTC(year, month - 1, day, hour, minute)),
  );
}

export function describeDeadline(
  endDateLocal: string,
  isContinuousIntake: boolean,
): string | null {
  if (isContinuousIntake) {
    return "Ten konkurs nie ma terminu zamknięcia. Wnioski są przyjmowane bez ograniczenia czasowego.";
  }

  if (endDateLocal === "") {
    return null;
  }

  return (
    `Nabór zamyka się ${formatLocalReading(endDateLocal)}. `
    + "Wnioski złożone później nie wejdą."
  );
}
