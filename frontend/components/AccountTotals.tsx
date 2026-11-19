import type { Account } from "@/lib/api";
import { money } from "@/lib/money";

// Any account can hold a negative balance (an overdraft, a credit card owed), so assets and debts
// are split by the sign of the balance, not by the account type.
export function AccountTotals({ accounts }: { accounts: Account[] }) {
  // Summed in whole cents: adding decimal dollars directly drifts (0.1 + 0.2 !== 0.3).
  let assetCents = 0;
  let debtCents = 0;
  for (const account of accounts) {
    const cents = Math.round(account.balance * 100);
    if (cents >= 0) assetCents += cents;
    else debtCents += cents;
  }

  const totals = [
    { label: "Net worth", cents: assetCents + debtCents },
    { label: "Assets", cents: assetCents },
    { label: "Debts", cents: debtCents },
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
