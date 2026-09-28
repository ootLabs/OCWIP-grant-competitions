import type { Metadata } from "next";

import { NotReadyView } from "@/components/not-ready-view";
import { PublicFrame } from "@/components/public-frame";

export const metadata: Metadata = {
  title: "Archiwum wyników | Generator konkursów OCWIP",
};

/** Placeholder for T-108 (archiwum rozstrzygniętych konkursów), see T-122x. */
export default function ArchivePage() {
  return (
    <PublicFrame>
      <NotReadyView title="Archiwum wyników" />
    </PublicFrame>
  );
}
