import { turnstileOrigin } from "./human-check";

/**
 * The security headers of every page (T-112, from T-47), built in one place
 * so middleware.ts and its test read the same policy.
 *
 * Scripts: only the site's own files and the inline ones carrying this
 * request's nonce ('strict-dynamic' lets them load the chunks they need);
 * Next.js puts the nonce on its own scripts when the request carries this
 * policy. Styles allow inline, because components set style attributes,
 * which no nonce can cover. connect-src is the site itself and the API.
 * The one frame is Cloudflare Turnstile's on the account forms; its script
 * is added by the site's own code, which 'strict-dynamic' already admits.
 */
export function contentSecurityPolicy({
  nonce,
  apiOrigin,
  development,
}: {
  nonce: string;
  apiOrigin: string | null;
  development: boolean;
}): string {
  const connect = ["'self'", ...(apiOrigin ? [apiOrigin] : []), ...(development ? ["ws:"] : [])];
  return [
    "default-src 'self'",
    // React Refresh evaluates code in development only.
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${development ? " 'unsafe-eval'" : ""}`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self' data:",
    `connect-src ${connect.join(" ")}`,
    `frame-src ${turnstileOrigin}`,
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ].join("; ");
}

/** No camera, microphone or position: nothing on this site asks for them. */
export const permissionsPolicy =
  "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

/** The origin of the API the browser calls, or null when it is the site itself (a relative address). */
export function originOf(address: string | undefined): string | null {
  if (!address) return null;
  try {
    return new URL(address).origin;
  } catch {
    return null;
  }
}
