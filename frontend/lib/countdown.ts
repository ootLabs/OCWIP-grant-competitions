/**
 * How much time is left until the intake closes, as a sentence (T-23).
 *
 * The card asks for a countdown so that nobody has to subtract two dates in
 * their head. It does NOT decide whether the intake is open: that answer comes
 * from the backend rule (T-21) and travels with every competition. This module
 * only ever renders a remaining span for a moment somebody else called open,
 * which is why it takes the closing instant and the current one as arguments
 * and reads no clock of its own.
 *
 * No seconds, on purpose. A ticking second counter on a deadline turns a page
 * somebody is reading into a page somebody is watching, it breaks a screen
 * reader that announces every change, and the cut off is to the minute anyway.
 */

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

export type Remaining = {
  readonly days: number;
  readonly hours: number;
  readonly minutes: number;
};

/**
 * The span between now and the closing moment, or null once it has passed.
 *
 * Null rather than a run of zeros: the minute the intake closes, the sentence
 * on the screen stops being "there is time left" and becomes the backend's
 * own "nabór został zamknięty", and a countdown showing "0 minut" next to it
 * would be a second, quieter answer to the same question.
 *
 * Truncated, not rounded. "2 dni" left with 2 days and 23 hours on the clock
 * understates the time somebody has, which is the direction a deadline may
 * safely be wrong in.
 */
export function remainingUntil(closesAt: string, now: Date): Remaining | null {
  const milliseconds = new Date(closesAt).getTime() - now.getTime();

  if (!Number.isFinite(milliseconds) || milliseconds <= 0) {
    return null;
  }

  return {
    days: Math.floor(milliseconds / DAY),
    hours: Math.floor((milliseconds % DAY) / HOUR),
    minutes: Math.floor((milliseconds % HOUR) / MINUTE),
  };
}

/**
 * Polish plural forms, which have three of them and get a product laughed at
 * when they are wrong: 1 dzien, 2 dni, 5 dni, 22 dni, 24 dni.
 */
function plural(
  count: number,
  one: string,
  few: string,
  many: string,
): string {
  const lastDigit = count % 10;
  const lastTwo = count % 100;

  if (count === 1) {
    return `${count} ${one}`;
  }

  if (lastDigit >= 2 && lastDigit <= 4 && (lastTwo < 12 || lastTwo > 14)) {
    return `${count} ${few}`;
  }

  return `${count} ${many}`;
}

/**
 * The span as the page prints it, coarsest unit first and at most two of them.
 *
 * Two units because "3 dni i 7 godzin" is a decision somebody can act on,
 * while "3 dni, 7 godzin i 12 minut" is a stopwatch. The minutes only show up
 * once they are the thing that matters, which is the last day.
 */
export function formatRemaining(remaining: Remaining): string {
  const { days, hours, minutes } = remaining;

  if (days > 0) {
    const daysText = plural(days, "dzień", "dni", "dni");

    return hours > 0
      ? `${daysText} i ${plural(hours, "godzina", "godziny", "godzin")}`
      : daysText;
  }

  if (hours > 0) {
    const hoursText = plural(hours, "godzina", "godziny", "godzin");

    return minutes > 0
      ? `${hoursText} i ${plural(minutes, "minuta", "minuty", "minut")}`
      : hoursText;
  }

  // Under a minute is still time somebody can submit in, and "0 minut" reads
  // as "too late" to a person who is not too late yet.
  return minutes > 0
    ? plural(minutes, "minuta", "minuty", "minut")
    : "mniej niż minuta";
}
