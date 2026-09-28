import type { Metadata } from "next";

import { NotReadyView } from "@/components/not-ready-view";
import { PublicFrame } from "@/components/public-frame";

export const metadata: Metadata = {
  title: "Deklaracja dostępności | Generator konkursów OCWIP",
};

/**
 * Placeholder for T-121: the real declaration needs the exact headings and
 * `id`s from "Warunki techniczne... deklaracji dostępności" v2.0 plus content
 * from OCWIP (PK-E), neither of which belongs in a placeholder card (T-122x).
 * The URL is fixed here already because the real card's own spec requires
 * this exact address, and the footer link (public-frame.tsx) already points
 * to it so no page ever links to a 404.
 */
export default function AccessibilityDeclarationPage() {
  return (
    <PublicFrame>
      <NotReadyView title="Deklaracja dostępności" />
    </PublicFrame>
  );
}
