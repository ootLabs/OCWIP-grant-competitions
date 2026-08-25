"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { SCREENS } from "@/lib/screens";

/**
 * Client side only because the active row is derived from the current route.
 * This is prototype chrome, not part of the design being shown: it stays
 * visually quiet so it does not compete with the screen on the right.
 */
export function ScreenNav() {
  const pathname = usePathname();

  return (
    <nav className="flex flex-col gap-1" aria-label="Ekrany prototypu">
      <Link
        href="/"
        className={`rounded-[var(--radius-sm)] px-3 py-2 text-[13px] font-semibold no-underline ${
          pathname === "/" ? "bg-surface-muted text-text" : "text-text-link"
        }`}
      >
        Przegląd
      </Link>

      {SCREENS.map((screen) => {
        const href = `/${screen.slug}`;
        const active = pathname === href;
        return (
          <Link
            key={screen.slug}
            href={href}
            aria-current={active ? "page" : undefined}
            className={`flex items-baseline gap-2 rounded-[var(--radius-sm)] px-3 py-2 text-[13px] no-underline ${
              active ? "bg-surface-muted font-semibold text-text" : "text-text-link"
            }`}
          >
            <span className="w-4 shrink-0 text-brand-accent-text">{screen.step}</span>
            <span>{screen.title}</span>
          </Link>
        );
      })}
    </nav>
  );
}
