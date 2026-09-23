import { PublicFrame } from "@/components/public-frame";

/** The frame around every public competition page (T-23). */
export default function PublicCompetitionsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <PublicFrame>{children}</PublicFrame>;
}
