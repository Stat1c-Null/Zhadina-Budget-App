"use client";

import { useEffect, useState, type FormEvent } from "react";
import { AppShell } from "@/components/AppShell";
import { Button } from "@/components/Button";
import { ErrorMessage, errorMessage } from "@/components/ErrorMessage";
import { TextField } from "@/components/TextField";
import { ApiError, getSettings, updateSettings, type FieldErrors, type Settings as SettingsData } from "@/lib/api";

export function Settings() {
  return <AppShell>{() => <SettingsForm />}</AppShell>;
}

function SettingsForm() {
  const [settings, setSettings] = useState<SettingsData | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    getSettings().then(setSettings, (error) => setLoadError(errorMessage(error)));
  }, []);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const priorEarnings = String(new FormData(event.currentTarget).get("priorEarnings") ?? "").trim();

    setFieldErrors({});
    setFormError(null);
    setSaved(false);
    setBusy(true);
    try {
      // An empty field clears the setting.
      setSettings(await updateSettings({ priorEarnings: priorEarnings === "" ? null : Number(priorEarnings) }));
      setSaved(true);
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        setFieldErrors(error.fieldErrors);
      } else {
        setFormError(errorMessage(error));
      }
    }
    setBusy(false);
  }

  if (loadError) {
    return (
      <div className="flex flex-col items-start gap-4">
        <ErrorMessage>{loadError}</ErrorMessage>
        <Button onClick={() => window.location.reload()}>Try again</Button>
      </div>
    );
  }

  if (!settings) {
    return <p className="text-muted">Loading…</p>;
  }

  return (
    <>
      <h1 className="text-3xl font-semibold">Settings</h1>

      <form onSubmit={onSubmit} noValidate className="mt-8 rounded-2xl border border-border bg-surface p-6">
        <div className="max-w-xs">
          <TextField
            label="Money you made before using Zhadina"
            name="priorEarnings"
            type="number"
            min="0"
            step="0.01"
            inputMode="decimal"
            placeholder="0.00"
            defaultValue={settings.priorEarnings?.toFixed(2)}
            onChange={() => setSaved(false)}
            errors={fieldErrors.priorEarnings}
          />
        </div>
        <p className="-mt-2 mb-4 text-sm text-muted">
          Shown on your dashboard. Leave it empty to clear it.
        </p>

        {formError && <ErrorMessage>{formError}</ErrorMessage>}

        <div className="flex items-center gap-4">
          <Button type="submit" disabled={busy}>
            {busy ? "Saving…" : "Save"}
          </Button>
          <p role="status" className="text-sm text-muted">
            {saved ? "Saved." : ""}
          </p>
        </div>
      </form>
    </>
  );
}
