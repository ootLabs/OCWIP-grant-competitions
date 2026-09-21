/**
 * How a date, an hour and an amount are written on a screen (T-23).
 *
 * One module, because the public competition page shows all three next to the
 * sentence the backend writes for the intake cut off, and two formatters with
 * slightly different ideas about what "25.10.2026" looks like read as two
 * different systems on one page.
 *
 * The shape deliberately mirrors CompetitionIntakeMessage on the backend:
 * digits rather than month names, the time zone said out loud, and UTC as the
 * honest fallback for an environment without time zone data. A deadline hour
 * that is one off is the single worst thing this page can print.
 */

/** The system serves one region, so "local time" is Polish time. */
export const POLISH_TIME_ZONE = "Europe/Warsaw";

export const POLISH_TIME_LABEL = "czasu polskiego";
export const UTC_TIME_LABEL = "czasu UTC";

/**
 * Whether this runtime can place an instant on a Polish wall clock.
 *
 * Asked once per call rather than assumed: a slim container image without the
 * ICU time zone database throws here, and the fallback below exists so that
 * such a build prints an honest UTC hour instead of a Polish sentence wrapped
 * around the wrong number.
 */
function hasPolishTimeZone(): boolean {
  try {
    new Intl.DateTimeFormat("pl-PL", { timeZone: POLISH_TIME_ZONE });
    return true;
  } catch {
    return false;
  }
}

function zone(): { timeZone: string; label: string } {
  return hasPolishTimeZone()
    ? { timeZone: POLISH_TIME_ZONE, label: POLISH_TIME_LABEL }
    : { timeZone: "UTC", label: UTC_TIME_LABEL };
}

/** The time zone the hours on this page are given in, named for the reader. */
export function timeZoneLabel(): string {
  return zone().label;
}

/**
 * An instant as a day, without an hour: "25.10.2026".
 *
 * Both this and formatMoment pin the time zone, which is what keeps a server
 * rendered page and its hydration in the browser printing the same string on
 * a machine set to another zone.
 */
export function formatDay(instant: string | Date): string {
  const { timeZone } = zone();

  return new Intl.DateTimeFormat("pl-PL", {
    timeZone,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(new Date(instant));
}

/** An instant as a day and an hour: "25.10.2026, 12:00". */
export function formatMoment(instant: string | Date): string {
  const { timeZone } = zone();

  return new Intl.DateTimeFormat("pl-PL", {
    timeZone,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(new Date(instant));
}

/**
 * A calendar date that never was an instant: "2027-03-31" to "31.03.2027".
 *
 * Parsed by hand instead of through Date, because `new Date("2027-03-31")`
 * reads as midnight UTC and then prints as the previous day for any reader
 * west of Greenwich. A project start date has no hour and must not acquire
 * one.
 */
export function formatDateOnly(date: string): string {
  const [year, month, day] = date.split("-");

  return `${day}.${month}.${year}`;
}

/**
 * An amount of money, in the currency every figure in this system is in.
 *
 * Values arrive from OpenAPI typed `number | string`, because the backend
 * writes decimals that do not always survive a double. Formatting takes both
 * and the string is the one that keeps the last grosz.
 */
export function formatAmount(amount: number | string): string {
  return new Intl.NumberFormat("pl-PL", {
    style: "currency",
    currency: "PLN",
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(Number(amount));
}

/** A percentage as the report writes it: "20%", not "20,00%". */
export function formatPercent(value: number | string): string {
  return new Intl.NumberFormat("pl-PL", {
    maximumFractionDigits: 2,
  }).format(Number(value)) + "%";
}

/**
 * A size limit as an operator wrote it: "10 MB".
 *
 * Megabytes of 1024 kilobytes, because that is the unit every upload dialog a
 * person has ever seen counts in, and a limit that reads as 10 MB in the
 * browser and 10,5 MB on our page is a support call.
 */
export function formatFileSize(bytes: number | string): string {
  const megabytes = Number(bytes) / (1024 * 1024);

  return `${new Intl.NumberFormat("pl-PL", {
    maximumFractionDigits: 1,
  }).format(megabytes)} MB`;
}
