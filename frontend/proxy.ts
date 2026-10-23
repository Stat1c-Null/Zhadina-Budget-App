import { NextResponse, type NextRequest } from "next/server";

// Must match options.Cookie.Name in backend/Zhadina.Api/Program.cs.
const AUTH_COOKIE = "zhadina_auth";

// Optimistic redirects only: this checks that the cookie exists, not that it's valid.
// The real check is the backend's /api/auth/me (see app/dashboard/Dashboard.tsx).
export function proxy(request: NextRequest) {
  const signedIn = request.cookies.has(AUTH_COOKIE);
  const { pathname } = request.nextUrl;

  if (pathname === "/" || (pathname.startsWith("/dashboard") && !signedIn)) {
    return NextResponse.redirect(new URL(signedIn ? "/dashboard" : "/login", request.url));
  }
  if ((pathname === "/login" || pathname === "/sign-up") && signedIn) {
    return NextResponse.redirect(new URL("/dashboard", request.url));
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/", "/dashboard/:path*", "/login", "/sign-up"],
};
