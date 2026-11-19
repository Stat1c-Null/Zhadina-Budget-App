"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { AccountTotals } from "@/components/AccountTotals";
import { AppShell } from "@/components/AppShell";
import { Button } from "@/components/Button";
import { ErrorMessage, errorMessage } from "@/components/ErrorMessage";
import { getSettings, listAccounts, type Account, type Settings } from "@/lib/api";
import { money } from "@/lib/money";

const LINK = "font-medium text-text underline decoration-plasma underline-offset-4 hover:decoration-ion";

export function Dashboard() {
  return (
    <AppShell>
      {(user) => (
        <>
          <h1 className="text-3xl font-semibold break-words">Welcome, {user.email}</h1>
          <p className="mt-2 text-muted">This is your dashboard.</p>

          <Overview />

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

function Overview() {
  const [data, setData] = useState<{ accounts: Account[]; settings: Settings } | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([listAccounts(), getSettings()]).then(
      ([accounts, settings]) => setData({ accounts, settings }),
      (error) => setLoadError(errorMessage(error)),
    );
  }, []);

  if (loadError) {
    return (
      <div className="mt-8 flex flex-col items-start gap-4">
        <ErrorMessage>{loadError}</ErrorMessage>
        <Button onClick={() => window.location.reload()}>Try again</Button>
      </div>
    );
  }

  if (!data) {
    return <p className="mt-8 text-muted">Loading…</p>;
  }

  const { priorEarnings } = data.settings;

  return (
    <>
      {data.accounts.length > 0 ? (
        <AccountTotals accounts={data.accounts} />
      ) : (
        <p className="mt-8 text-muted">
          No accounts yet.{" "}
          <Link href="/accounts" className={LINK}>
            Add one
          </Link>{" "}
          to see your net worth, assets, debts, savings and investments here.
        </p>
      )}

      {/* Same grid as the totals, so this card lines up under the first one. */}
      <section aria-label="Earned before Zhadina" className="mt-4 grid gap-4 sm:grid-cols-3">
        <div className="rounded-2xl border border-border bg-surface px-6 py-4">
          <p className="text-sm text-muted">Earned before Zhadina</p>
          {priorEarnings === null ? (
            <p className="mt-1 text-muted">
              Not set.{" "}
              <Link href="/settings" className={LINK}>
                Add it in Settings
              </Link>
            </p>
          ) : (
            <p className="mt-1 text-2xl font-semibold tabular-nums break-words">{money.format(priorEarnings)}</p>
          )}
        </div>
      </section>
    </>
  );
}
