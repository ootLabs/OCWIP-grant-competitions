import { mailpitUrl } from "./env";

interface Summary {
  readonly ID: string;
  readonly Subject: string;
}

/**
 * The newest message to an address with this subject, read from Mailpit's
 * API (T-100): the test follows the same link a person would click, and
 * never reads a token from the database.
 */
export async function waitForMail(to: string, subject: string, timeoutMs = 30_000): Promise<string> {
  const query = encodeURIComponent(`to:"${to}" subject:"${subject}"`);
  const until = Date.now() + timeoutMs;

  while (Date.now() < until) {
    const response = await fetch(`${mailpitUrl}/api/v1/search?query=${query}`);
    if (response.ok) {
      const { messages } = (await response.json()) as { messages: Summary[] };
      const found = messages.find((message) => message.Subject === subject);
      if (found) {
        const message = (await (await fetch(`${mailpitUrl}/api/v1/message/${found.ID}`)).json()) as { Text: string };
        return message.Text;
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }

  throw new Error(`No mail "${subject}" to ${to} within ${timeoutMs / 1000} s.`);
}

/**
 * A mail body with the no-break spaces turned into ordinary ones, so an
 * assertion can be written the way a person reads the sentence.
 *
 * Amounts in mails and in refusals go through PolishNumbers.Amount on the
 * backend, which writes "5 700,00 zł" with no-break spaces, exactly as the
 * screens do: a kwota has no business breaking across two lines. An
 * assertion spelling the amount with a plain space passes locally in the unit
 * tests and fails here, which is how it was found.
 */
export function plainSpaces(text: string): string {
  return text.replace(/\u00a0/g, " ");
}

/** The first link in a mail that goes to this path of the site. */
export function linkTo(body: string, path: string): string {
  const match = new RegExp(`https?://\\S+${path.replace(/[/]/g, "\\/")}\\S*`).exec(body);
  if (!match) {
    throw new Error(`No link to ${path} in:\n${body}`);
  }
  return match[0];
}
