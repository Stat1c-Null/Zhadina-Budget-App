"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { AppShell } from "@/components/AppShell";
import { Button } from "@/components/Button";
import { ErrorMessage, errorMessage } from "@/components/ErrorMessage";
import { Modal } from "@/components/Modal";
import { SelectField } from "@/components/SelectField";
import { TextField } from "@/components/TextField";
import { netBalance } from "@/lib/accounts";
import {
  ApiError,
  createTransaction,
  deleteTransaction,
  listAccounts,
  listTransactions,
  updateTransaction,
  type Account,
  type FieldErrors,
  type Transaction,
  type TransactionCategory,
  type TransactionChange,
  type TransactionInput,
} from "@/lib/api";
import { money } from "@/lib/money";
import {
  CATEGORIES_BY_KIND,
  CATEGORY_LABELS,
  KIND_LABELS,
  formatDate,
  localToday,
  newestFirst,
  withNewBalances,
  type EntryKind,
} from "@/lib/transactions";

const ENTRY_KINDS: EntryKind[] = ["Expense", "Income"];

// Which modal is open. `transaction` is set when editing. Only one is mounted at a time, which keeps field ids unique.
type OpenModal = { form: "entry" | "transfer"; transaction?: Transaction } | null;

export function Transactions() {
  return <AppShell>{() => <TransactionList />}</AppShell>;
}

function TransactionList() {
  const [accounts, setAccounts] = useState<Account[] | null>(null);
  const [transactions, setTransactions] = useState<Transaction[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [modal, setModal] = useState<OpenModal>(null);

  useEffect(() => {
    Promise.all([listAccounts(), listTransactions()]).then(
      ([loadedAccounts, loadedTransactions]) => {
        setAccounts(loadedAccounts);
        setTransactions(loadedTransactions);
      },
      (error) => setLoadError(errorMessage(error)),
    );
  }, []);

  function onSaved({ transaction, accounts: changed }: TransactionChange) {
    setAccounts((current) => current && withNewBalances(current, changed));
    setTransactions((current) =>
      newestFirst(
        current?.some((t) => t.id === transaction.id)
          ? current.map((t) => (t.id === transaction.id ? transaction : t))
          : [transaction, ...(current ?? [])],
      ),
    );
  }

  function onDeleted(id: string, changed: Account[]) {
    setAccounts((current) => current && withNewBalances(current, changed));
    setTransactions((current) => current?.filter((t) => t.id !== id) ?? null);
  }

  if (loadError) {
    return (
      <div className="flex flex-col items-start gap-4">
        <ErrorMessage>{loadError}</ErrorMessage>
        <Button onClick={() => window.location.reload()}>Try again</Button>
      </div>
    );
  }

  if (!accounts || !transactions) {
    return <p className="text-muted">Loading…</p>;
  }

  const accountName = (id: string | null) => accounts.find((account) => account.id === id)?.name ?? "Deleted account";

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-3xl font-semibold">Transactions</h1>
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => setModal({ form: "entry" })} disabled={accounts.length === 0}>
            Add transaction
          </Button>
          <Button variant="secondary" onClick={() => setModal({ form: "transfer" })} disabled={accounts.length < 2}>
            Transfer
          </Button>
        </div>
      </div>

      {accounts.length === 0 && (
        <p className="mt-8 text-muted">
          Add an account first on the{" "}
          <Link href="/accounts" className="text-text underline underline-offset-2 hover:text-primary">
            Accounts page
          </Link>
          , then record its transactions here.
        </p>
      )}
      {accounts.length === 1 && <p className="mt-2 text-sm text-muted">Transfers need at least two accounts.</p>}

      {accounts.length > 0 && transactions.length === 0 && (
        <p className="mt-8 text-muted">No transactions yet. Add an expense or income, or record a transfer.</p>
      )}

      {transactions.length > 0 && (
        <ul aria-label="Transaction history" className="mt-8 divide-y divide-border rounded-2xl border border-border bg-surface">
          {transactions.map((transaction) => (
            <li key={transaction.id} className="px-6 py-4">
              <TransactionRow
                transaction={transaction}
                accountName={accountName}
                onEdit={() =>
                  setModal({ form: transaction.kind === "Transfer" ? "transfer" : "entry", transaction })
                }
                onDeleted={(changed) => onDeleted(transaction.id, changed)}
              />
            </li>
          ))}
        </ul>
      )}

      {modal?.form === "entry" && (
        <Modal title={modal.transaction ? "Edit transaction" : "Add transaction"} onClose={() => setModal(null)}>
          {(close) => (
            <EntryForm
              accounts={accounts}
              transaction={modal.transaction}
              onSaved={(change) => {
                onSaved(change);
                close();
              }}
              onCancel={close}
            />
          )}
        </Modal>
      )}
      {modal?.form === "transfer" && (
        <Modal title={modal.transaction ? "Edit transfer" : "Transfer between accounts"} onClose={() => setModal(null)}>
          {(close) => (
            <TransferForm
              accounts={accounts}
              transaction={modal.transaction}
              onSaved={(change) => {
                onSaved(change);
                close();
              }}
              onCancel={close}
            />
          )}
        </Modal>
      )}
    </>
  );
}

function TransactionRow({
  transaction,
  accountName,
  onEdit,
  onDeleted,
}: {
  transaction: Transaction;
  accountName: (id: string | null) => string;
  onEdit: () => void;
  onDeleted: (changed: Account[]) => void;
}) {
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const { kind, amount, category, description } = transaction;
  const categoryLabel = category ? CATEGORY_LABELS[category] : null;
  const title = description ?? categoryLabel ?? KIND_LABELS[kind];
  const where =
    kind === "Transfer"
      ? `${accountName(transaction.accountId)} → ${accountName(transaction.toAccountId)}`
      : accountName(transaction.accountId);
  // The category is already the title when there's no description.
  const details = [formatDate(transaction.date), where, description && categoryLabel].filter(Boolean).join(" · ");
  const shown =
    kind === "Expense" ? money.format(-amount) : kind === "Income" ? `+${money.format(amount)}` : money.format(amount);

  async function onDelete() {
    setBusy(true);
    setError(null);
    try {
      onDeleted((await deleteTransaction(transaction.id)).accounts);
    } catch (caught) {
      setError(errorMessage(caught));
      setBusy(false);
    }
  }

  return (
    <>
      {error && <ErrorMessage>{error}</ErrorMessage>}
      <div className="flex flex-wrap items-center gap-x-6 gap-y-3">
        <div className="min-w-0 flex-1 basis-48">
          <p className="font-medium break-words">{title}</p>
          <p className="text-sm text-muted">
            {kind !== "Expense" && <span className="sr-only">{KIND_LABELS[kind]} · </span>}
            {details}
          </p>
        </div>
        <span className={`text-xl tabular-nums ${kind === "Expense" ? "text-danger" : kind === "Transfer" ? "text-muted" : ""}`}>
          {shown}
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
            <Button variant="secondary" onClick={onEdit} aria-label={`Edit ${title}`}>
              Edit
            </Button>
            <Button variant="secondary" onClick={() => setConfirming(true)} aria-label={`Delete ${title}`}>
              Delete
            </Button>
          </div>
        )}
      </div>
    </>
  );
}

// Shared by both forms: sends the input, then shows field errors under their fields or one message for the form.
function useSave(transaction: Transaction | undefined, onSaved: (change: TransactionChange) => void) {
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function save(input: TransactionInput) {
    setFieldErrors({});
    setFormError(null);
    setBusy(true);
    try {
      onSaved(await (transaction ? updateTransaction(transaction.id, input) : createTransaction(input)));
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        setFieldErrors(error.fieldErrors);
      } else {
        setFormError(errorMessage(error));
      }
      setBusy(false);
    }
  }

  return { fieldErrors, formError, busy, save };
}

// Amount, date and description, read the same way by both forms.
function readCommonFields(event: FormEvent<HTMLFormElement>) {
  const form = new FormData(event.currentTarget);
  const amount = String(form.get("amount") ?? "").trim();
  return {
    amount: amount === "" ? null : Number(amount),
    date: String(form.get("date") ?? ""),
    description: String(form.get("description") ?? ""),
  };
}

// Each option shows the balance the way it counts in the totals, so a credit card reads as money owed.
function accountOptions(accounts: Account[]) {
  return accounts.map((account) => (
    <option key={account.id} value={account.id}>
      {account.name} ({money.format(netBalance(account))})
    </option>
  ));
}

function EntryForm({
  accounts,
  transaction,
  onSaved,
  onCancel,
}: {
  accounts: Account[];
  transaction?: Transaction;
  onSaved: (change: TransactionChange) => void;
  onCancel: () => void;
}) {
  const { fieldErrors, formError, busy, save } = useSave(transaction, onSaved);
  const [kind, setKind] = useState<EntryKind>(transaction?.kind === "Income" ? "Income" : "Expense");
  const [accountId, setAccountId] = useState(transaction?.accountId ?? (accounts.length === 1 ? accounts[0].id : ""));
  const [category, setCategory] = useState<TransactionCategory | "">(transaction?.category ?? "");
  const isCreditCard = accounts.find((account) => account.id === accountId)?.type === "CreditCard";
  const kindErrors = fieldErrors.kind;

  // A category from the other list isn't allowed, so switching the kind clears it.
  function onKindChange(next: EntryKind) {
    setKind(next);
    if (category && !CATEGORIES_BY_KIND[next].includes(category)) setCategory("");
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    save({ kind, accountId, toAccountId: null, category: category || null, ...readCommonFields(event) });
  }

  return (
    <form onSubmit={onSubmit} noValidate>
      <fieldset className="mb-4" aria-describedby={kindErrors?.length ? "kind-error" : undefined}>
        <legend className="mb-1 text-sm font-medium">Type</legend>
        <div className="inline-flex rounded-lg border border-border bg-bg p-1">
          {ENTRY_KINDS.map((value) => (
            <label
              key={value}
              className="cursor-pointer rounded-md px-4 py-1.5 text-sm font-medium transition-colors has-[:checked]:bg-primary has-[:checked]:text-white has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-primary/60 not-has-[:checked]:hover:bg-border/50"
            >
              <input
                type="radio"
                name="kind"
                value={value}
                checked={kind === value}
                onChange={() => onKindChange(value)}
                className="sr-only"
              />
              {KIND_LABELS[value]}
            </label>
          ))}
        </div>
        <FieldErrorList id="kind-error" errors={kindErrors} />
      </fieldset>

      <div className="grid gap-x-4 sm:grid-cols-2">
        <TextField
          label="Amount"
          name="amount"
          type="number"
          step="0.01"
          min="0.01"
          inputMode="decimal"
          defaultValue={transaction?.amount.toFixed(2)}
          required
          errors={fieldErrors.amount}
        />
        <TextField
          label="Date"
          name="date"
          type="date"
          defaultValue={transaction?.date ?? localToday()}
          required
          errors={fieldErrors.date}
        />
      </div>

      <SelectField
        label="Account"
        name="accountId"
        value={accountId}
        onChange={(event) => setAccountId(event.target.value)}
        required
        errors={fieldErrors.accountId}
      >
        <option value="" disabled>
          Choose an account
        </option>
        {accountOptions(accounts)}
      </SelectField>
      {isCreditCard && (
        <p className="-mt-2 mb-4 text-sm text-muted">
          {kind === "Expense"
            ? "An expense adds to what you owe on this card."
            : "Income on a card, such as a refund, lowers what you owe."}
        </p>
      )}

      <SelectField
        label="Category"
        name="category"
        value={category}
        onChange={(event) => setCategory(event.target.value as TransactionCategory | "")}
        errors={fieldErrors.category}
      >
        <option value="">No category</option>
        {CATEGORIES_BY_KIND[kind].map((value) => (
          <option key={value} value={value}>
            {CATEGORY_LABELS[value]}
          </option>
        ))}
      </SelectField>

      <TextField
        label="Description (optional)"
        name="description"
        defaultValue={transaction?.description ?? ""}
        maxLength={200}
        autoComplete="off"
        errors={fieldErrors.description}
      />

      <FormButtons formError={formError} busy={busy} onCancel={onCancel}>
        {transaction ? "Save changes" : "Add transaction"}
      </FormButtons>
    </form>
  );
}

function TransferForm({
  accounts,
  transaction,
  onSaved,
  onCancel,
}: {
  accounts: Account[];
  transaction?: Transaction;
  onSaved: (change: TransactionChange) => void;
  onCancel: () => void;
}) {
  const { fieldErrors, formError, busy, save } = useSave(transaction, onSaved);
  const [fromId, setFromId] = useState(transaction?.accountId ?? "");
  const [toId, setToId] = useState(transaction?.toAccountId ?? "");
  const isCard = (id: string) => accounts.find((account) => account.id === id)?.type === "CreditCard";

  // "To" leaves out the "from" account, so picking that one as "from" clears "to".
  function onFromChange(next: string) {
    setFromId(next);
    if (next === toId) setToId("");
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    save({ kind: "Transfer", accountId: fromId, toAccountId: toId || null, category: null, ...readCommonFields(event) });
  }

  return (
    <form onSubmit={onSubmit} noValidate>
      <div className="grid gap-x-4 sm:grid-cols-2">
        <SelectField
          label="From"
          name="accountId"
          value={fromId}
          onChange={(event) => onFromChange(event.target.value)}
          required
          errors={fieldErrors.accountId}
        >
          <option value="" disabled>
            Choose an account
          </option>
          {accountOptions(accounts)}
        </SelectField>
        <SelectField
          label="To"
          name="toAccountId"
          value={toId}
          onChange={(event) => setToId(event.target.value)}
          required
          errors={fieldErrors.toAccountId}
        >
          <option value="" disabled>
            Choose an account
          </option>
          {accountOptions(accounts.filter((account) => account.id !== fromId))}
        </SelectField>
      </div>
      {(isCard(toId) || isCard(fromId)) && (
        <p className="-mt-2 mb-4 text-sm text-muted">
          {isCard(toId) && "Paying a credit card lowers what you owe. "}
          {isCard(fromId) && "Moving money off a credit card adds to what you owe."}
        </p>
      )}

      <div className="grid gap-x-4 sm:grid-cols-2">
        <TextField
          label="Amount"
          name="amount"
          type="number"
          step="0.01"
          min="0.01"
          inputMode="decimal"
          defaultValue={transaction?.amount.toFixed(2)}
          required
          errors={fieldErrors.amount}
        />
        <TextField
          label="Date"
          name="date"
          type="date"
          defaultValue={transaction?.date ?? localToday()}
          required
          errors={fieldErrors.date}
        />
      </div>

      <TextField
        label="Description (optional)"
        name="description"
        defaultValue={transaction?.description ?? ""}
        maxLength={200}
        autoComplete="off"
        errors={fieldErrors.description}
      />

      <FormButtons formError={formError} busy={busy} onCancel={onCancel}>
        {transaction ? "Save changes" : "Record transfer"}
      </FormButtons>
    </form>
  );
}

function FieldErrorList({ id, errors }: { id: string; errors?: string[] }) {
  if (!errors?.length) return null;
  return (
    <ul id={id} className="mt-1 text-sm text-danger">
      {errors.map((error) => (
        <li key={error}>{error}</li>
      ))}
    </ul>
  );
}

function FormButtons({
  formError,
  busy,
  onCancel,
  children,
}: {
  formError: string | null;
  busy: boolean;
  onCancel: () => void;
  children: ReactNode;
}) {
  return (
    <>
      {formError && <ErrorMessage>{formError}</ErrorMessage>}
      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" disabled={busy}>
          {busy ? "Saving…" : children}
        </Button>
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
      </div>
    </>
  );
}
