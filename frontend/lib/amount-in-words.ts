/**
 * An amount of money written out in Polish words (T-22, step 1.4: "Pod każdym
 * polem kwotowym dopisujemy kwotę słownie, bo wchodzi potem do umowy").
 *
 * Kept general rather than tied to the wizard, because the same wording is
 * meant for an agreement document (T-45), not only for a hint under an input.
 *
 * Handles zero to 999 999 999 (below a billion, comfortably above any grant
 * this system will ever award) and the grosze that follow a comma. Polish
 * numerals decline by grammatical case and gender in general, but an amount
 * of money only ever needs the nominative, which is what this writes.
 */

const ONES = [
  "zero", "jeden", "dwa", "trzy", "cztery", "pięć", "sześć", "siedem",
  "osiem", "dziewięć",
];

const TEEN = [
  "dziesięć", "jedenaście", "dwanaście", "trzynaście", "czternaście",
  "piętnaście", "szesnaście", "siedemnaście", "osiemnaście", "dziewiętnaście",
];

const TENS = [
  "", "", "dwadzieścia", "trzydzieści", "czterdzieści", "pięćdziesiąt",
  "sześćdziesiąt", "siedemdziesiąt", "osiemdziesiąt", "dziewięćdziesiąt",
];

const HUNDREDS = [
  "", "sto", "dwieście", "trzysta", "czterysta", "pięćset", "sześćset",
  "siedemset", "osiemset", "dziewięćset",
];

/**
 * A scale word (thousand, million) in its three Polish plural forms, chosen
 * by the same rule as CURRENCY_WORDS below.
 */
interface ScaleWord {
  singular: string;
  few: string;
  many: string;
}

const THOUSAND: ScaleWord = {
  singular: "tysiąc",
  few: "tysiące",
  many: "tysięcy",
};

const MILLION: ScaleWord = {
  singular: "milion",
  few: "miliony",
  many: "milionów",
};

const ZLOTY: ScaleWord = { singular: "złoty", few: "złote", many: "złotych" };
const GROSZ: ScaleWord = { singular: "grosz", few: "grosze", many: "groszy" };

/**
 * Polish plural class of a whole number, the rule every one of the words
 * above follows: 1 is singular, 2 to 4 (but not 12 to 14) is "few", anything
 * else, including 0, is "many".
 */
function pluralForm(n: number): keyof ScaleWord {
  if (n === 1) {
    return "singular";
  }

  const lastTwo = n % 100;
  const lastOne = n % 10;

  if (lastOne >= 2 && lastOne <= 4 && !(lastTwo >= 12 && lastTwo <= 14)) {
    return "few";
  }

  return "many";
}

function word(forms: ScaleWord, n: number): string {
  return forms[pluralForm(n)];
}

/**
 * 1 to 999 in words. Never called with 0: every caller below only reaches
 * this function guarded by its own "is there anything in this group" check.
 */
function belowThousand(n: number): string {
  const parts: string[] = [];

  const hundreds = Math.floor(n / 100);
  const rest = n % 100;

  if (hundreds > 0) {
    parts.push(HUNDREDS[hundreds]);
  }

  if (rest >= 10 && rest <= 19) {
    parts.push(TEEN[rest - 10]);
  } else {
    const tens = Math.floor(rest / 10);
    const ones = rest % 10;

    if (tens >= 2) {
      parts.push(TENS[tens]);
    }

    // "jeden" only stands alone or after tens/hundreds when it is genuinely
    // the number one (21 "dwadzieścia jeden"), never dropped: unlike English
    // "twenty" it is not implied.
    if (ones > 0) {
      parts.push(ONES[ones]);
    }
  }

  return parts.join(" ");
}

/** A whole, non-negative integer in Polish words, grouped by thousand. */
export function integerInWords(value: number): string {
  if (!Number.isFinite(value) || value < 0 || !Number.isInteger(value)) {
    throw new RangeError(
      "integerInWords oczekuje nieujemnej liczby całkowitej.",
    );
  }

  if (value === 0) {
    return "zero";
  }

  if (value >= 1_000_000_000) {
    throw new RangeError("integerInWords obsługuje liczby poniżej miliarda.");
  }

  const millions = Math.floor(value / 1_000_000);
  const thousands = Math.floor((value % 1_000_000) / 1000);
  const rest = value % 1000;

  const groups: string[] = [];

  if (millions > 0) {
    // Neither "jeden milion" nor "jeden tysiąc" below is said: a lone group
    // is just the scale word by itself.
    const prefix = millions === 1 ? "" : `${belowThousand(millions)} `;
    groups.push(`${prefix}${word(MILLION, millions)}`);
  }

  if (thousands > 0) {
    const prefix = thousands === 1 ? "" : `${belowThousand(thousands)} `;
    groups.push(`${prefix}${word(THOUSAND, thousands)}`);
  }

  if (rest > 0) {
    groups.push(belowThousand(rest));
  }

  return groups.join(" ");
}

/**
 * An amount of money in words: "sto dwadzieścia tysięcy złotych" or, with
 * grosze, "sto złotych i pięćdziesiąt groszy".
 *
 * Accepts what the rest of the system hands a money field: a JS number or the
 * string OpenAPI sometimes sends for a decimal that would not survive a
 * double (see format.ts, formatAmount). Grosze are read off two decimal
 * digits, rounded rather than truncated, so 12.005 reads as 12 złotych 1
 * grosz and not a grosz short.
 */
export function amountInWords(amount: number | string): string {
  const numeric = Number(amount);

  if (!Number.isFinite(numeric) || numeric < 0) {
    throw new RangeError("amountInWords oczekuje nieujemnej kwoty.");
  }

  // Rounded to the grosz before splitting, so floating point noise (0.1 +
  // 0.2 territory) never turns a round amount into "99 groszy" plus a
  // złoty that is one too many.
  const totalGrosze = Math.round(numeric * 100);
  const zloty = Math.floor(totalGrosze / 100);
  const grosze = totalGrosze % 100;

  const zlotyPhrase = `${integerInWords(zloty)} ${word(ZLOTY, zloty)}`;

  if (grosze === 0) {
    return zlotyPhrase;
  }

  const groszePhrase = `${integerInWords(grosze)} ${word(GROSZ, grosze)}`;

  return `${zlotyPhrase} i ${groszePhrase}`;
}
