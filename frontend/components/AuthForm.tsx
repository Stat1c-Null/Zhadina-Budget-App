"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { ApiError, login, signUp, type FieldErrors } from "@/lib/api";
import { Button } from "./Button";
import { TextField } from "./TextField";

type Mode = "login" | "sign-up";

const COPY = {
  login: { submit: "Log in", busy: "Logging in…", switchText: "No account yet?", switchLink: "Sign up", switchHref: "/sign-up" },
  "sign-up": { submit: "Create account", busy: "Creating account…", switchText: "Already have an account?", switchLink: "Log in", switchHref: "/login" },
} as const;

export function AuthForm({ mode }: { mode: Mode }) {
  const router = useRouter();
  const [fieldErrors, setFieldErrors] = useState<FieldErrors & { confirm?: string[] }>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const copy = COPY[mode];

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const email = String(form.get("email") ?? "");
    const password = String(form.get("password") ?? "");

    setFieldErrors({});
    setFormError(null);

    if (mode === "sign-up" && password !== form.get("confirm")) {
      setFieldErrors({ confirm: ["Passwords don't match."] });
      return;
    }

    setBusy(true);
    try {
      await (mode === "login" ? login(email, password) : signUp(email, password));
      router.replace("/dashboard");
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        setFieldErrors(error.fieldErrors);
      } else {
        setFormError(error instanceof ApiError ? error.message : "Can't reach the server. Try again.");
      }
      setBusy(false);
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate>
      <TextField label="Email" name="email" type="email" autoComplete="email" required errors={fieldErrors.email} />
      <TextField
        label="Password"
        name="password"
        type="password"
        autoComplete={mode === "login" ? "current-password" : "new-password"}
        required
        errors={fieldErrors.password}
      />
      {mode === "sign-up" && (
        <TextField
          label="Confirm password"
          name="confirm"
          type="password"
          autoComplete="new-password"
          required
          errors={fieldErrors.confirm}
        />
      )}

      {formError && (
        <p role="alert" className="mb-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger">
          {formError}
        </p>
      )}

      <Button type="submit" disabled={busy} className="w-full">
        {busy ? copy.busy : copy.submit}
      </Button>

      <p className="mt-6 text-center text-sm text-muted">
        {copy.switchText}{" "}
        <Link href={copy.switchHref} className="font-medium text-primary hover:underline">
          {copy.switchLink}
        </Link>
      </p>
    </form>
  );
}
