// Typed wrappers for the C# auth API (backend/Zhadina.Api/Features/Auth).
// Requests are same-origin; next.config.ts rewrites /api/* to the backend.

export type User = { id: string; email: string };

export type FieldErrors = Partial<Record<"email" | "password", string[]>>;

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
  429: "Too many attempts. Wait a minute and try again.",
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api/auth${path}`, {
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

const post = <T>(path: string, body?: unknown) =>
  request<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });

export const signUp = (email: string, password: string) => post<User>("/sign-up", { email, password });
export const login = (email: string, password: string) => post<User>("/login", { email, password });
export const signOut = () => post<void>("/sign-out");
export const me = () => request<User>("/me");
