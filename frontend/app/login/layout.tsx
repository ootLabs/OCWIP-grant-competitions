import { PublicFrame } from "@/components/public-frame";

/** The sign in screen stands in the same frame as the competition pages. */
export default function LoginLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <PublicFrame>{children}</PublicFrame>;
}
