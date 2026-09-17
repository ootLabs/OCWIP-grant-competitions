/**
 * Who is signed in, asked once and in one place.
 *
 * The session lives in an HttpOnly cookie issued by the API origin (T-12.3),
 * so nothing in the browser and nothing rendering on the Next.js server can
 * read it: "is there a cookie" is not a question this front can answer, and it
 * would be the wrong question anyway, because a logout rotates the security
 * stamp and a cookie that still exists can already be worthless. The only
 * honest answer comes from asking the backend, which is what GET /me is for.
 */

import { ApiError, apiFetch } from "./api-client";
import type { components } from "./api-schema";

export type CurrentUser = components["schemas"]["CurrentUserResponse"];
export type Role = components["schemas"]["Role"];

/** Where a caller without a live session is sent. */
export const loginPath = "/login";

/**
 * The signed in account, or null when there is no session.
 *
 * Null rather than a thrown error, because "nobody is signed in" is a normal
 * state of this endpoint and not a failure: it is what every first visit to a
 * panel looks like. Everything else still throws, so a backend that is down
 * does not get mistaken for a session that expired and silently log somebody
 * out (docs/architektura.md: a 404 answers 404, precisely so that 401 keeps
 * meaning one thing).
 */
export async function fetchCurrentUser(): Promise<CurrentUser | null> {
  try {
    return await apiFetch<CurrentUser>("/me");
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return null;
    }

    throw error;
  }
}

/**
 * Ends the session on the server and answers when it is safe to leave.
 *
 * Failures are swallowed on purpose. Somebody pressing "wyloguj" on a shared
 * machine has to end up off this screen even when the request does not land;
 * leaving them inside the panel with an error message is the one outcome the
 * button exists to prevent. POST /logout is idempotent and anonymous (T-12.3),
 * so a retry after a reconnect costs nothing.
 */
export async function logout(): Promise<void> {
  try {
    await apiFetch("/logout", { method: "POST" });
  } catch {
    // Deliberately empty, see above.
  }
}

/**
 * What the header calls the signed in user.
 *
 * An applicant acts as a Podmiot, not as a person, so the entity is the name
 * that belongs on their screens. Accounts with no entity (operator, reviewer)
 * fall back to the person, because they are one.
 */
export function accountLabel(user: CurrentUser): string {
  return user.entityName ?? `${user.firstName} ${user.lastName}`;
}
