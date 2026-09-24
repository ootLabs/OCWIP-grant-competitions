import { PublicFrame } from "@/components/public-frame";

/**
 * The account screens (T-12.7, T-12.8): sign in, registration, confirming the
 * address, the password reset. A route group, so the frame is written once and
 * none of the addresses carry "(account)".
 */
export default function AccountLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <PublicFrame>{children}</PublicFrame>;
}
