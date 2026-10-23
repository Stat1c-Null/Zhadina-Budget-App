import type { InputHTMLAttributes } from "react";

type Props = InputHTMLAttributes<HTMLInputElement> & { label: string; name: string; errors?: string[] };

export function TextField({ label, name, errors, ...input }: Props) {
  const errorId = `${name}-error`;
  const hasErrors = !!errors?.length;

  return (
    <div className="mb-4">
      <label htmlFor={name} className="mb-1 block text-sm font-medium">
        {label}
      </label>
      <input
        id={name}
        name={name}
        aria-invalid={hasErrors}
        aria-describedby={hasErrors ? errorId : undefined}
        className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-text placeholder:text-muted/60 focus:border-primary focus:ring-2 focus:ring-primary/40 focus:outline-none aria-invalid:border-danger"
        {...input}
      />
      {hasErrors && (
        <ul id={errorId} className="mt-1 text-sm text-danger">
          {errors.map((error) => (
            <li key={error}>{error}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
