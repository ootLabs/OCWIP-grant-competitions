import { HumanCheckProvider } from "@/components/human-check";
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
  // Read per request (every page here renders on request, for the CSP
  // nonce), so the key is the server's setting, not one baked into the build.
  return (
    <PublicFrame>
      <HumanCheckProvider siteKey={process.env.TURNSTILE_SITE_KEY ?? null}>{children}</HumanCheckProvider>
    </PublicFrame>
  );
}
