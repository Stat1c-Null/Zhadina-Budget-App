"use client";

import { AppShell } from "@/components/AppShell";

export function Dashboard() {
  return (
    <AppShell>
      {(user) => (
        <>
          <h1 className="text-3xl font-semibold break-words">Welcome, {user.email}</h1>
          <p className="mt-2 text-muted">This is your dashboard.</p>

          <section className="mt-8 rounded-2xl border border-border bg-surface p-6">
            <h2 className="text-lg font-semibold">Budgets</h2>
            <p className="mt-1 text-sm text-muted">
              Coming soon. Budgets and transactions arrive in the next iteration.
            </p>
          </section>
        </>
      )}
    </AppShell>
  );
}
