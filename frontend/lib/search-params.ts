/**
 * Reading the address of an account screen on the server (T-12.7, T-12.8).
 *
 * The screens read their parameters in page.tsx and pass them down as props,
 * rather than with useSearchParams in the form: that hook needs a Suspense
 * boundary to render at all.
 */

/** What Next.js hands a page as searchParams, once awaited. */
export type SearchParams = Record<string, string | string[] | undefined>;

/**
 * One value of a parameter, or null when it is missing or empty.
 *
 * A parameter written twice arrives as an array, and the first one is what the
 * link that brought the visitor here wrote.
 */
export function firstParam(value: string | string[] | undefined): string | null {
  const first = Array.isArray(value) ? value[0] : value;

  return first === undefined || first === "" ? null : first;
}
