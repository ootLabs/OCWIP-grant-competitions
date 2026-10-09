"use client";

import { createContext, useContext, useEffect, useRef, useState } from "react";

import { loadTurnstile } from "@/lib/human-check";

/**
 * The widget's site key, read on the server from TURNSTILE_SITE_KEY at
 * request time (app/(account)/layout.tsx), so one built image fits every
 * domain. Null switches the check off, the way it is off in the tests.
 */
const SiteKey = createContext<string | null>(null);

export function HumanCheckProvider({
  siteKey,
  children,
}: {
  siteKey: string | null;
  children: React.ReactNode;
}) {
  return <SiteKey.Provider value={siteKey || null}>{children}</SiteKey.Provider>;
}

const failedMessage =
  "Nie udało się wczytać weryfikacji. Sprawdź połączenie i odśwież stronę.";

/**
 * A form's Turnstile check. The token is good for one request, so a form
 * calls renew() after every answer that leaves it on screen; the widget is
 * then drawn afresh and earns a new one, usually without a click.
 *
 * waiting is true until the widget has handed over a token, and the form
 * keeps its button off until then: a request without one is refused anyway.
 */
export function useHumanCheck(action: string) {
  const siteKey = useContext(SiteKey);
  const [token, setToken] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  return {
    token,
    waiting: siteKey !== null && token === null,
    renew: () => {
      setToken(null);
      setAttempt((current) => current + 1);
    },
    widget:
      siteKey === null ? null : (
        <TurnstileWidget
          action={action}
          attempt={attempt}
          onToken={setToken}
          siteKey={siteKey}
        />
      ),
  };
}

function TurnstileWidget({
  siteKey,
  action,
  attempt,
  onToken,
}: {
  siteKey: string;
  action: string;
  attempt: number;
  onToken: (token: string | null) => void;
}) {
  const container = useRef<HTMLDivElement>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let current = true;
    let widgetId: string | undefined;

    loadTurnstile()
      .then((turnstile) => {
        if (!current || container.current === null) {
          return;
        }
        widgetId = turnstile.render(container.current, {
          sitekey: siteKey,
          action,
          language: "pl",
          callback: (token) => {
            setFailed(false);
            onToken(token);
          },
          // Five minutes on, a token Cloudflare would refuse: the button
          // goes off again until the widget has a new one.
          "expired-callback": () => onToken(null),
          "error-callback": () => {
            onToken(null);
            setFailed(true);
          },
        });
      })
      .catch(() => current && setFailed(true));

    return () => {
      current = false;
      if (widgetId !== undefined) {
        window.turnstile?.remove(widgetId);
      }
    };
  }, [siteKey, action, attempt, onToken]);

  return (
    <div className="flex flex-col gap-1">
      {/* Fixed height, so the button below does not jump when the frame arrives. */}
      <div className="min-h-[65px]" ref={container} />
      {failed ? (
        <p className="text-sm text-brand-accent-text" role="alert">
          {failedMessage}
        </p>
      ) : null}
    </div>
  );
}
