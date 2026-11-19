import { netBalance } from "@/lib/accounts";
import type { Account } from "@/lib/api";
import { money } from "@/lib/money";

// Assets and debts are split by the sign of each account's net balance: a credit card always counts as debt,
// and any other account does when it's overdrawn (see netBalance).
// Savings and investments add up every account the user marked that way. Credit cards can't be marked.
export function AccountTotals({ accounts }: { accounts: Account[] }) {
  // Summed in whole cents: adding decimal dollars directly drifts (0.1 + 0.2 !== 0.3).
  let assetCents = 0;
  let debtCents = 0;
  let savingsCents = 0;
  let investmentCents = 0;
  for (const account of accounts) {
    const cents = Math.round(netBalance(account) * 100);
    if (cents >= 0) assetCents += cents;
    else debtCents += cents;
    if (account.purpose === "Savings") savingsCents += cents;
    else if (account.purpose === "Investment") investmentCents += cents;
  }

  // Five cards in three columns: savings and investments start the second row.
  const totals = [
    { label: "Net worth", cents: assetCents + debtCents },
    { label: "Assets", cents: assetCents },
    { label: "Debts", cents: debtCents },
    { label: "Savings", cents: savingsCents },
    { label: "Investments", cents: investmentCents },
  ];

  return (
    <section aria-label="Totals" className="mt-8 grid gap-4 sm:grid-cols-3">
      {totals.map(({ label, cents }) => (
        <div key={label} className="rounded-2xl border border-border bg-surface px-6 py-4">
          <p className="text-sm text-muted">{label}</p>
          <p className={`mt-1 text-2xl font-semibold tabular-nums break-words ${cents < 0 ? "text-danger" : ""}`}>
            {money.format(cents / 100)}
          </p>
        </div>
      ))}
    </section>
  );
}
