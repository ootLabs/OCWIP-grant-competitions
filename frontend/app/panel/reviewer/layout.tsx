import type { Metadata } from "next";
import { ReviewerPanel } from "./reviewer-panel";

export const metadata: Metadata = {
  title: "Panel recenzenta | Generator konkursów OCWIP",
};

/** Server side wrapper for the metadata, the same split as the other two panels. */
export default function ReviewerPanelLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return <ReviewerPanel>{children}</ReviewerPanel>;
}
