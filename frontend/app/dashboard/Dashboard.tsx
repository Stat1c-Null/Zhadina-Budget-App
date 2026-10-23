"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Button } from "@/components/Button";
import { ApiError, me, signOut, type User } from "@/lib/api";

export function Dashboard() {
  const router = useRouter();
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
      <header className="flex items-center justify-between border-b border-border px-6 py-4">
        <span className="font-semibold text-primary">Zhadina Budget</span>
        <div className="flex items-center gap-4">
          <span className="hidden text-sm text-muted sm:inline">{user.email}</span>
          <Button onClick={onSignOut}>Sign out</Button>
        </div>
      </header>

      <main className="mx-auto w-full max-w-4xl flex-1 px-6 py-10">
        <h1 className="text-3xl font-semibold">Welcome, {user.email}</h1>
        <p className="mt-2 text-muted">This is your dashboard.</p>

        <section className="mt-8 rounded-2xl border border-border bg-surface p-6">
          <h2 className="text-lg font-semibold">Budgets</h2>
          <p className="mt-1 text-sm text-muted">Coming soon. Budgets and transactions arrive in the next iteration.</p>
        </section>
      </main>
    </div>
  );
}
