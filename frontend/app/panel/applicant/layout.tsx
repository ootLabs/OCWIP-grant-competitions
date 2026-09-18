import type { Metadata } from "next";
import { ApplicantPanel } from "./applicant-panel";

export const metadata: Metadata = {
  title: "Panel wnioskodawcy | Generator konkursów OCWIP",
};

/**
 * Server side wrapper, so the panel keeps its metadata.
 *
 * The frame itself has to be a client component (it asks GET /me and reacts to
 * the answer), and a client component cannot export metadata. Splitting it in
 * two is cheaper than losing the page title on every applicant screen.
 */
export default function ApplicantPanelLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return <ApplicantPanel>{children}</ApplicantPanel>;
}
