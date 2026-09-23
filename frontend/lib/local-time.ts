/**
 * The wall clock the operator types into the competition wizard, turned into
 * the UTC instant the API stores (T-22, step 1.1: "Operator wpisuje czas
 * lokalny", mirrors CompetitionIntakeMessage's "local time is Polish time" on
 * the backend, see format.ts).
 *
 * The wizard never converts the other way. The raw string an
 * `<input type="datetime-local">` gives back stays in the draft untouched, so
 * a half filled step shows exactly what was typed. Conversion happens once,
 * at the moment a value is sent to the backend.
 */

import { POLISH_TIME_ZONE } from "./format";

/**
 * A local wall clock reading ("2026-09-12T12:00", the shape an
 * `<input type="datetime-local">` gives back) as a UTC instant, ISO
 * formatted.
 *
 * Works out the UTC offset for Europe/Warsaw AT that moment rather than
 * assuming +01:00 or +02:00, so a date on either side of the October or
 * March change converts correctly without the caller having to know which
 * side it falls on.
 */
export function localWarsawToUtcIso(localValue: string): string {
  const naiveUtc = parseAsIfUtc(localValue);
  const offsetMinutes = offsetAt(naiveUtc, POLISH_TIME_ZONE);

  return new Date(naiveUtc.getTime() - offsetMinutes * 60_000).toISOString();
}

/**
 * "2026-09-12T12:00" read as though the digits were already UTC. Not the
 * answer, only the seed offsetAt needs to find the real offset.
 */
function parseAsIfUtc(localValue: string): Date {
  const [datePart, timePart = "00:00"] = localValue.split("T");
  const [year, month, day] = datePart.split("-").map(Number);
  const [hour, minute] = timePart.split(":").map(Number);

  return new Date(Date.UTC(year, month - 1, day, hour, minute));
}

/**
 * Minutes to ADD to a UTC instant to reach the same instant's wall clock
 * reading in `timeZone`. Positive for a zone ahead of UTC, which Warsaw
 * always is.
 *
 * Reads the offset from what the instant actually formats as, through
 * `Intl.DateTimeFormat`, rather than a fixed +01:00/+02:00 table: that is
 * what makes the two days around each DST change come out right without a
 * calendar of transition dates baked in here.
 */
function offsetAt(instant: Date, timeZone: string): number {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone,
    hourCycle: "h23",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  })
    .formatToParts(instant)
    .reduce<Record<string, string>>((fields, part) => {
      if (part.type !== "literal") {
        fields[part.type] = part.value;
      }
      return fields;
    }, {});

  // Midnight in "hour: 2-digit, hourCycle: h23" can come back as "24"
  // depending on the runtime's ICU data; folded back to 0 so it does not
  // roll the date forward by a day.
  const hour = Number(parts.hour) % 24;

  const readAsUtc = Date.UTC(
    Number(parts.year),
    Number(parts.month) - 1,
    Number(parts.day),
    hour,
    Number(parts.minute),
    Number(parts.second),
  );

  return (readAsUtc - instant.getTime()) / 60_000;
}
