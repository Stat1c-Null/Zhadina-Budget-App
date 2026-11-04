"use client";

import { useEffect, useState, type FormEvent } from "react";
import { AppShell } from "@/components/AppShell";
import { Button } from "@/components/Button";
import { SelectField } from "@/components/SelectField";
import { TextField } from "@/components/TextField";
import {
  ApiError,
  createAccount,
  deleteAccount,
  listAccounts,
  updateAccount,
  type Account,
  type AccountType,
  type FieldErrors,
} from "@/lib/api";

// No currency symbol: accounts don't store a currency yet.
const money = new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

// Also sets the order of the options in the form.
const ACCOUNT_TYPE_LABELS: Record<AccountType, string> = {
  Bank: "Bank",
  Cash: "Cash",
  Investment: "Investment",
  RothIra: "Roth IRA",
  TraditionalIra: "Traditional IRA",
  Retirement401k: "401(k)",
  CreditCard: "Credit card",
  Other: "Other",
};

const NEW_ACCOUNT = "new";

function errorMessage(error: unknown) {
  // The session ended while the page was open. Reloading lets AppShell clear the cookie and go to /login.
  if (error instanceof ApiError && error.status === 401) window.location.reload();
  return error instanceof ApiError ? error.message : "Can't reach the server. Try again.";
}

function ErrorMessage({ children }: { children: string }) {
  return (
    <p role="alert" className="mb-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger">
      {children}
    </p>
  );
}

export function Accounts() {
  return <AppShell>{() => <AccountList />}</AppShell>;
}

function AccountList() {
  const [accounts, setAccounts] = useState<Account[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  // Which form is open: an account's id, NEW_ACCOUNT, or none. One at a time keeps field ids unique.
  const [editing, setEditing] = useState<string | null>(null);

  useEffect(() => {
    listAccounts().then(setAccounts, (error) => setLoadError(errorMessage(error)));
  }, []);

  function onSaved(saved: Account) {
    setAccounts((current) =>
      current?.some((account) => account.id === saved.id)
        ? current.map((account) => (account.id === saved.id ? saved : account))
        : [...(current ?? []), saved],
    );
    setEditing(null);
  }

  function onDeleted(id: string) {
    setAccounts((current) => current?.filter((account) => account.id !== id) ?? null);
  }

  if (loadError) {
    return (
      <div className="flex flex-col items-start gap-4">
        <ErrorMessage>{loadError}</ErrorMessage>
        <Button onClick={() => window.location.reload()}>Try again</Button>
      </div>
    );
  }

  if (!accounts) {
    return <p className="text-muted">Loading…</p>;
  }

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-3xl font-semibold">Accounts</h1>
        {editing !== NEW_ACCOUNT && <Button onClick={() => setEditing(NEW_ACCOUNT)}>Add account</Button>}
      </div>

      {editing === NEW_ACCOUNT && (
        <section aria-label="Add account" className="mt-8 rounded-2xl border border-border bg-surface p-6">
          <AccountForm onSaved={onSaved} onCancel={() => setEditing(null)} />
        </section>
      )}

      {accounts.length === 0 && editing !== NEW_ACCOUNT && (
        <p className="mt-8 text-muted">Add your first account and set how much money is in it.</p>
      )}

      {accounts.length > 0 && (
        <ul className="mt-8 divide-y divide-border rounded-2xl border border-border bg-surface">
          {accounts.map((account) => (
            <li key={account.id} className="px-6 py-4">
              {editing === account.id ? (
                <AccountForm account={account} onSaved={onSaved} onCancel={() => setEditing(null)} />
              ) : (
                <AccountRow
                  account={account}
                  onEdit={() => setEditing(account.id)}
                  onDeleted={() => onDeleted(account.id)}
                />
              )}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

function AccountRow({
  account,
  onEdit,
  onDeleted,
}: {
  account: Account;
  onEdit: () => void;
  onDeleted: () => void;
}) {
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onDelete() {
    setBusy(true);
    setError(null);
    try {
      await deleteAccount(account.id);
      onDeleted();
    } catch (caught) {
      setError(errorMessage(caught));
      setBusy(false);
    }
  }

  return (
    <>
      {error && <ErrorMessage>{error}</ErrorMessage>}
      <div className="flex flex-wrap items-center gap-x-6 gap-y-3">
        <div className="min-w-0 flex-1 basis-40">
          <p className="font-medium break-words">{account.name}</p>
          <p className="text-sm text-muted">{ACCOUNT_TYPE_LABELS[account.type]}</p>
        </div>
        <span className={`text-xl tabular-nums ${account.balance < 0 ? "text-danger" : ""}`}>
          {money.format(account.balance)}
        </span>
        {confirming ? (
          <div className="flex items-center gap-2">
            <Button variant="danger" onClick={onDelete} disabled={busy}>
              {busy ? "Deleting…" : "Confirm delete"}
            </Button>
            <Button variant="secondary" onClick={() => setConfirming(false)} disabled={busy}>
              Keep
            </Button>
          </div>
        ) : (
          <div className="flex items-center gap-2">
            <Button variant="secondary" onClick={onEdit} aria-label={`Edit ${account.name}`}>
              Edit
            </Button>
            <Button variant="secondary" onClick={() => setConfirming(true)} aria-label={`Delete ${account.name}`}>
              Delete
            </Button>
          </div>
        )}
      </div>
    </>
  );
}

function AccountForm({
  account,
  onSaved,
  onCancel,
}: {
  account?: Account;
  onSaved: (account: Account) => void;
  onCancel: () => void;
}) {
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const balance = String(form.get("balance") ?? "").trim();
    const input = {
      name: String(form.get("name") ?? ""),
      type: String(form.get("type") ?? "") as AccountType | "",
      balance: balance === "" ? null : Number(balance),
    };

    setFieldErrors({});
    setFormError(null);
    setBusy(true);
    try {
      onSaved(await (account ? updateAccount(account.id, input) : createAccount(input)));
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        setFieldErrors(error.fieldErrors);
      } else {
        setFormError(errorMessage(error));
      }
      setBusy(false);
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate>
      <div className="grid gap-x-4 sm:grid-cols-[1fr_11rem_10rem]">
        <TextField
          label="Account name"
          name="name"
          defaultValue={account?.name}
          maxLength={100}
          autoComplete="off"
          autoFocus
          required
          errors={fieldErrors.name}
        />
        <SelectField
          label="Account type"
          name="type"
          defaultValue={account?.type ?? ""}
          required
          errors={fieldErrors.type}
        >
          <option value="" disabled>
            Choose a type
          </option>
          {Object.entries(ACCOUNT_TYPE_LABELS).map(([type, label]) => (
            <option key={type} value={type}>
              {label}
            </option>
          ))}
        </SelectField>
        <TextField
          label="Balance"
          name="balance"
          type="number"
          step="0.01"
          inputMode="decimal"
          defaultValue={account?.balance.toFixed(2)}
          required
          errors={fieldErrors.balance}
        />
      </div>

      {formError && <ErrorMessage>{formError}</ErrorMessage>}

      <div className="flex items-center gap-2">
        <Button type="submit" disabled={busy}>
          {busy ? "Saving…" : account ? "Save changes" : "Add account"}
        </Button>
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
