"use client";

import { useEffect } from "react";

/**
 * Puts the keyboard where a list of gaps points (T-34, T-50a): the field's
 * wrapper is scrolled into view and the first control inside it gets the
 * focus, then the caller clears the target so the next click on the same
 * position works again.
 *
 * Shared by the application's draft and the report, because clicking a gap
 * has to land on the field in both: a list of what is missing that only
 * scrolls is a list a keyboard user cannot follow.
 *
 * @param target the id of a field wrapper (`fieldAnchorId`), or null for none
 * @param sectionKey the section on screen; the effect re-runs when it changes,
 *   because the target mounts only once its section is the current one
 * @param onDone called after the jump, to drop the target
 */
export function useFieldFocus(
  target: string | null,
  sectionKey: string,
  onDone: () => void,
): void {
  useEffect(() => {
    if (target === null) {
      return;
    }

    const element = document.getElementById(target);
    // Optional even on the method itself: jsdom (the tests of both callers)
    // has no scrollIntoView at all, and calling it unconditionally would
    // throw before the focus below, the part a keyboard user needs.
    element?.scrollIntoView?.({ block: "center" });
    // The wrapper itself (field-view.tsx, table-field.tsx) is not a control
    // a browser will focus: the actual input inside it is what a keyboard
    // user needs to land on.
    element?.querySelector<HTMLElement>("input, textarea, select, button")?.focus({
      preventScroll: true,
    });
    onDone();
    // Re-runs once the section the target lives on has actually mounted.
  }, [target, sectionKey, onDone]);
}
