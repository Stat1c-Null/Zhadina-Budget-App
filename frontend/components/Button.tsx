import type { ButtonHTMLAttributes } from "react";

export function Button({ className = "", ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      className={`rounded-lg bg-primary px-4 py-2 font-medium text-white transition-colors hover:bg-primary-hover focus-visible:ring-2 focus-visible:ring-primary/60 focus-visible:ring-offset-2 focus-visible:ring-offset-bg focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-60 ${className}`}
      {...props}
    />
  );
}
