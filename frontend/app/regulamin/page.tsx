import type { Metadata } from "next";

import { LegalDocumentPage } from "@/components/legal-document";

export const metadata: Metadata = {
  title: "Regulamin serwisu | Generator konkursów OCWIP",
};

/** The terms of the service (T-121), the text accepted at registration. */
export default function TermsPage() {
  return <LegalDocumentPage kind="terms" />;
}
