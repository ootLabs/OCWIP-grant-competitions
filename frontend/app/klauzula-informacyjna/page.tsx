import type { Metadata } from "next";

import { LegalDocumentPage } from "@/components/legal-document";

export const metadata: Metadata = {
  title: "Klauzula informacyjna | Generator konkursów OCWIP",
};

/** The privacy notice (T-121), the text accepted at registration. */
export default function PrivacyPage() {
  return <LegalDocumentPage kind="privacy" />;
}
