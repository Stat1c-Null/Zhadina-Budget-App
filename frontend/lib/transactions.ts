import type { Account, Transaction, TransactionCategory, TransactionKind } from "@/lib/api";

export type EntryKind = Exclude<TransactionKind, "Transfer">;

export const KIND_LABELS: Record<TransactionKind, string> = {
  Expense: "Expense",
  Income: "Income",
  Transfer: "Transfer",
};

export const CATEGORY_LABELS: Record<TransactionCategory, string> = {
  Groceries: "Groceries",
  Dining: "Dining",
  Housing: "Housing",
  Utilities: "Utilities",
  Transport: "Transport",
  Health: "Health",
  Shopping: "Shopping",
  Entertainment: "Entertainment",
  Subscriptions: "Subscriptions",
  Travel: "Travel",
  Education: "Education",
  Fees: "Fees",
  Salary: "Salary",
  Business: "Business",
  Interest: "Interest",
  InvestmentIncome: "Investment income",
  Refunds: "Refunds",
  Gifts: "Gifts",
  Other: "Other",
};

// Must match TransactionCategories in backend/Zhadina.Api/Data/TransactionCategory.cs. Also the order in the form.
export const CATEGORIES_BY_KIND: Record<EntryKind, TransactionCategory[]> = {
  Expense: [
    "Groceries",
    "Dining",
    "Housing",
    "Utilities",
    "Transport",
    "Health",
    "Shopping",
    "Entertainment",
    "Subscriptions",
    "Travel",
    "Education",
    "Gifts",
    "Fees",
    "Other",
  ],
  Income: ["Salary", "Business", "Interest", "InvestmentIncome", "Gifts", "Refunds", "Other"],
};

const pad = (n: number) => String(n).padStart(2, "0");

// Today in the browser's time zone, as YYYY-MM-DD. toISOString() gives the UTC date instead,
// which in the evening west of UTC is already tomorrow.
export function localToday(now = new Date()): string {
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

// The time zone is fixed to UTC because new Date("YYYY-MM-DD") is midnight UTC,
// which west of UTC would otherwise show as the day before.
const dateFormat = new Intl.DateTimeFormat("en-US", { year: "numeric", month: "short", day: "numeric", timeZone: "UTC" });

export function formatDate(date: string): string {
  return dateFormat.format(new Date(`${date}T00:00:00Z`));
}

// Newest first, like the API. sort() is stable, so transactions on the same day keep their order,
// which puts a newly added one first among its day, as the API does.
export function newestFirst(transactions: Transaction[]): Transaction[] {
  return [...transactions].sort((a, b) => b.date.localeCompare(a.date));
}

// The balances the API sent back replace the ones on the page.
export function withNewBalances(accounts: Account[], changed: Account[]): Account[] {
  return accounts.map((account) => changed.find((c) => c.id === account.id) ?? account);
}
