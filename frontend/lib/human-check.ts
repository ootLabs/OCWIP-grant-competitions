/**
 * Cloudflare Turnstile on the account forms: the token the widget hands out
 * and the header it travels in. The backend checks it with Cloudflare before
 * the request reaches its handler (Configuration/HumanCheckConfiguration.cs).
 */

/** The same name the backend reads (HumanCheckConfiguration.TokenHeader). */
export const humanCheckHeader = "X-Turnstile-Token";

/** The refusal's ProblemDetails type (HumanCheckConfiguration.ProblemType). */
export const humanCheckProblemType = "urn:ocwip:problem:human-check";

/** Where the widget's script comes from; the CSP lets its frame in (security-headers.ts). */
export const turnstileOrigin = "https://challenges.cloudflare.com";

/** The request headers carrying the token, none when the check is off. */
export function humanCheckHeaders(token: string | null | undefined): Record<string, string> {
  return token ? { [humanCheckHeader]: token } : {};
}

export type TurnstileRenderOptions = {
  sitekey: string;
  action?: string;
  language?: string;
  callback?: (token: string) => void;
  "expired-callback"?: () => void;
  "error-callback"?: () => void;
};

export type Turnstile = {
  render: (container: HTMLElement, options: TurnstileRenderOptions) => string | undefined;
  remove: (widgetId: string) => void;
};

declare global {
  interface Window {
    turnstile?: Turnstile;
  }
}

let loading: Promise<Turnstile> | null = null;

/**
 * The widget's script, once per page. Added from code rather than as a tag,
 * so the CSP's 'strict-dynamic' admits it as loaded by the site's own code.
 */
export function loadTurnstile(): Promise<Turnstile> {
  if (window.turnstile) {
    return Promise.resolve(window.turnstile);
  }

  loading ??= new Promise<Turnstile>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = `${turnstileOrigin}/turnstile/v0/api.js?render=explicit`;
    script.async = true;
    script.onload = () =>
      window.turnstile ? resolve(window.turnstile) : reject(new Error("Turnstile did not start."));
    script.onerror = () => {
      // The next form gets to try again, rather than inheriting this failure.
      loading = null;
      script.remove();
      reject(new Error("Turnstile could not be loaded."));
    };
    document.head.appendChild(script);
  });

  return loading;
}
