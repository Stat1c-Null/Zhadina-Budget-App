// Typed wrappers for the C# API (backend/Zhadina.Api/Features).
// Requests are same-origin; next.config.ts rewrites /api/* to the backend.

export type User = { id: string; email: string };

// Matches the member names of the C# AccountType enum, which is what the API stores and sends.
export type AccountType =
  | "Bank"
  | "Cash"
  | "Investment"
  | "RothIra"
  | "TraditionalIra"
  | "Retirement401k"
  | "CreditCard"
  | "Other";

// Matches the C# AccountPurpose enum. Spending is what an account is when neither savings nor investment is ticked.
export type AccountPurpose = "Spending" | "Savings" | "Investment";

export type Account = { id: string; name: string; type: AccountType; balance: number; purpose: AccountPurpose };

// type is "" and balance is null when left empty, so the backend's validation can report them.
export type AccountInput = {
  name: string;
  type: AccountType | "";
  balance: number | null;
  purpose: AccountPurpose;
};

// Matches the C# TransactionKind enum.
export type TransactionKind = "Expense" | "Income" | "Transfer";

// Matches the C# TransactionCategory enum. Which kind each belongs to is in lib/transactions.ts.
export type TransactionCategory =
  | "Groceries"
  | "Dining"
  | "Housing"
  | "Utilities"
  | "Transport"
  | "Health"
  | "Shopping"
  | "Entertainment"
  | "Subscriptions"
  | "Travel"
  | "Education"
  | "Fees"
  | "Salary"
  | "Business"
  | "Interest"
  | "InvestmentIncome"
  | "Refunds"
  | "Gifts"
  | "Other";

// accountId is the account the money leaves on a transfer; toAccountId is set only on transfers.
// date is "YYYY-MM-DD", a calendar day with no time zone.
export type Transaction = {
  id: string;
  kind: TransactionKind;
  accountId: string;
  toAccountId: string | null;
  amount: number;
  date: string;
  category: TransactionCategory | null;
  description: string | null;
};

// accountId is "" and amount null when left empty, so the backend's validation can report them.
export type TransactionInput = Omit<Transaction, "id" | "amount" | "description"> & {
  amount: number | null;
  description: string;
};

// Saving answers with the accounts it touched, at their new balances.
export type TransactionChange = { transaction: Transaction; accounts: Account[] };

// Each setting is null until the user saves one. Saving sends every setting; null clears it.
export type Settings = { priorEarnings: number | null };

export type FieldErrors = Partial<Record<string, string[]>>;

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly fieldErrors: FieldErrors = {},
  ) {
    super(message);
  }
}

const FALLBACK_MESSAGES: Record<number, string> = {
  404: "This no longer exists. Reload the page to see the latest.",
  429: "Too many attempts. Wait a minute and try again.",
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: { "Content-Type": "application/json" },
    credentials: "same-origin",
  });

  if (response.ok) {
    return (response.status === 204 ? undefined : await response.json()) as T;
  }

  // The backend answers errors with RFC 7807 ProblemDetails.
  const problem = await response.json().catch(() => ({}));
  throw new ApiError(
    response.status,
    FALLBACK_MESSAGES[response.status] ?? problem.detail ?? problem.title ?? "Something went wrong.",
    problem.errors ?? {},
  );
}

const send = <T>(method: string, path: string, body?: unknown) =>
  request<T>(path, { method, body: body === undefined ? undefined : JSON.stringify(body) });

export const signUp = (email: string, password: string) =>
  send<User>("POST", "/api/auth/sign-up", { email, password });
export const login = (email: string, password: string) => send<User>("POST", "/api/auth/login", { email, password });
export const signOut = () => send<void>("POST", "/api/auth/sign-out");
export const me = () => request<User>("/api/auth/me");

export const listAccounts = () => request<Account[]>("/api/accounts");
export const createAccount = (input: AccountInput) =>
  send<Account>("POST", "/api/accounts", input);
export const updateAccount = (id: string, input: AccountInput) =>
  send<Account>("PUT", `/api/accounts/${id}`, input);
export const deleteAccount = (id: string) => send<void>("DELETE", `/api/accounts/${id}`);

export const listTransactions = () => request<Transaction[]>("/api/transactions");
export const createTransaction = (input: TransactionInput) =>
  send<TransactionChange>("POST", "/api/transactions", input);
export const updateTransaction = (id: string, input: TransactionInput) =>
  send<TransactionChange>("PUT", `/api/transactions/${id}`, input);
export const deleteTransaction = (id: string) =>
  send<{ accounts: Account[] }>("DELETE", `/api/transactions/${id}`);

export const getSettings = () => request<Settings>("/api/settings");
export const updateSettings = (settings: Settings) => send<Settings>("PUT", "/api/settings", settings);
