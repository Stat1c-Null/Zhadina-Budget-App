"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState, type ReactNode } from "react";
import { Button } from "@/components/Button";
import { ApiError, me, signOut, type User } from "@/lib/api";

const NAV_LINKS = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/accounts", label: "Accounts" },
];

// The frame around every signed-in page: checks the session, then renders the header and the page.
export function AppShell({ children }: { children: (user: User) => ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [user, setUser] = useState<User | null>(null);
  const [unreachable, setUnreachable] = useState(false);

  useEffect(() => {
    // The backend is the real auth check; proxy.ts only looks at whether the cookie exists.
    me().then(setUser, async (error) => {
      // Only a real 401 means the session is gone. For anything else (API down) we keep the
      // cookie: redirecting would loop, since proxy.ts sends cookie holders back here.
      if (!(error instanceof ApiError && error.status === 401)) {
        setUnreachable(true);
        return;
      }
      // Clear the stale cookie first, otherwise proxy.ts would bounce /login straight back here.
      await signOut().catch(() => {});
      router.replace("/login");
    });
  }, [router]);

  async function onSignOut() {
    await signOut().catch(() => {});
    router.replace("/login");
  }

  if (unreachable) {
    return (
      <main className="flex flex-1 flex-col items-center justify-center gap-4 text-muted">
        <p role="alert">Can&apos;t reach the server right now.</p>
        <Button onClick={() => window.location.reload()}>Try again</Button>
      </main>
    );
  }

  if (!user) {
    return <main className="flex flex-1 items-center justify-center text-muted">Loading…</main>;
  }

  return (
    <div className="flex flex-1 flex-col">
      <header className="flex flex-wrap items-center justify-between gap-x-6 gap-y-2 border-b border-border px-6 py-4">
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
          <span className="font-semibold text-primary">Zhadina Budget</span>
          <nav aria-label="Main" className="flex items-center gap-4 text-sm">
            {NAV_LINKS.map(({ href, label }) => (
              <Link
                key={href}
                href={href}
                aria-current={pathname === href ? "page" : undefined}
                className="text-muted transition-colors hover:text-text aria-[current=page]:font-medium aria-[current=page]:text-text"
              >
                {label}
              </Link>
            ))}
          </nav>
        </div>
        <div className="flex items-center gap-4">
          <span className="hidden text-sm text-muted sm:inline">{user.email}</span>
          <Button onClick={onSignOut}>Sign out</Button>
        </div>
      </header>

      <main className="mx-auto w-full max-w-4xl flex-1 px-6 py-10">{children(user)}</main>
    </div>
  );
}
