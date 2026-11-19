import { ApiError } from "@/lib/api";

// Turns a failed API call into text for ErrorMessage. Only call it from client code.
export function errorMessage(error: unknown) {
  // The session ended while the page was open. Reloading lets AppShell clear the cookie and go to /login.
  if (error instanceof ApiError && error.status === 401) window.location.reload();
  return error instanceof ApiError ? error.message : "Can't reach the server. Try again.";
}

export function ErrorMessage({ children }: { children: string }) {
  return (
    <p role="alert" className="mb-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger">
      {children}
    </p>
  );
}
