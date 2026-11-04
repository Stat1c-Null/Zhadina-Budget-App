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

export type Account = { id: string; name: string; type: AccountType; balance: number };

// type is "" and balance is null when left empty, so the backend's validation can report them.
export type AccountInput = { name: string; type: AccountType | ""; balance: number | null };

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
