"use client";

import { useEffect, useState } from "react";

/**
 * The address of this competition, ready to be copied somewhere else (T-23).
 *
 * The link is permanent by design on the backend side: an archived competition
 * keeps its address and only a draft has none, so a post made the day a call
 * opens still works the year after. This is the front of that promise, the
 * part an operator actually uses when they paste the call into Facebook.
 *
 * A read only input rather than a line of text with a button: copying by hand
 * has to stay possible for anybody whose browser refuses the clipboard API,
 * and an input is the one element a keyboard user can select all of.
 */
export function CompetitionPermalink({ path }: { path: string }) {
  // The absolute address is only knowable in the browser. Until then the
  // field holds the path, which is still correct, just not pasteable.
  const [url, setUrl] = useState(path);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    setUrl(new URL(path, window.location.origin).toString());
  }, [path]);

  async function copy() {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
    } catch {
      // No clipboard permission, no clipboard API, or an insecure origin. The
      // field is still there to be selected by hand, so this is not an error
      // worth an alarm.
      setCopied(false);
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <label className="text-sm font-semibold" htmlFor="permalink">
        Stały odnośnik do tego konkursu
      </label>
      <div className="flex flex-col gap-2 sm:flex-row">
        <input
          className="w-full rounded-sm border border-border-control bg-surface-muted px-3 py-2 text-sm"
          id="permalink"
          readOnly
          value={url}
          onFocus={(event) => event.currentTarget.select()}
        />
        <button
          className="rounded-sm border border-brand-accent px-4 py-2 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg"
          type="button"
          onClick={copy}
        >
          Kopiuj odnośnik
        </button>
      </div>
      {/* Announced, because a copy that leaves the screen unchanged is a copy
          nobody can tell happened. */}
      <p aria-live="polite" className="text-sm">
        {copied ? "Skopiowano odnośnik do schowka." : null}
      </p>
    </div>
  );
}
