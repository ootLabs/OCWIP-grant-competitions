import { NextResponse, type NextRequest } from "next/server";

import { contentSecurityPolicy, originOf, permissionsPolicy } from "@/lib/security-headers";

/**
 * T-112: a fresh nonce and the security headers on every page. The policy
 * travels on the request as well, which is where Next.js reads the nonce to
 * put on its own inline scripts; the layout reads it from x-nonce.
 */
export function middleware(request: NextRequest) {
  const nonce = btoa(crypto.randomUUID());
  const policy = contentSecurityPolicy({
    nonce,
    apiOrigin: originOf(process.env.NEXT_PUBLIC_API_URL),
    development: process.env.NODE_ENV === "development",
  });

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-nonce", nonce);
  requestHeaders.set("Content-Security-Policy", policy);

  const response = NextResponse.next({ request: { headers: requestHeaders } });
  response.headers.set("Content-Security-Policy", policy);
  response.headers.set("Permissions-Policy", permissionsPolicy);
  response.headers.set("X-Content-Type-Options", "nosniff");
  response.headers.set("Referrer-Policy", "no-referrer");
  return response;
}

export const config = {
  // Pages only: the built files and the icon need no nonce.
  matcher: [{ source: "/((?!_next/static|_next/image|favicon.ico).*)" }],
};
