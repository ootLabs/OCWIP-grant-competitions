import { PublicFrame } from "@/components/public-frame";

/** The frame around every public competition page (T-23). */
export default function PublicCompetitionsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  // Wide: the list lays cards side by side and a competition page has a
  // column of its own for the deadline and the way in.
  return <PublicFrame width="wide">{children}</PublicFrame>;
}
