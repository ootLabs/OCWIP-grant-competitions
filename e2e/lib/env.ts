/** Where the stack under test answers; the defaults are the local compose ports. */
export const apiUrl = process.env.E2E_API_URL ?? "http://localhost:8080";
export const mailpitUrl = process.env.E2E_MAILPIT_URL ?? "http://localhost:8025";

/** One run's own suffix, so the scenario also runs on a database that is not empty. */
export const run = process.env.E2E_RUN ?? Date.now().toString(36);
