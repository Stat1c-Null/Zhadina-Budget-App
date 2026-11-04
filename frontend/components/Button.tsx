import type { ButtonHTMLAttributes } from "react";

const VARIANTS = {
  primary: "bg-primary text-white hover:bg-primary-hover",
  secondary: "border border-border text-text hover:bg-border/50",
  danger: "border border-danger/60 text-danger hover:bg-danger/10",
  hud: "bg-bg/45 text-white hover:bg-bg/70",
};

type Props = ButtonHTMLAttributes<HTMLButtonElement> & { variant?: keyof typeof VARIANTS };

export function Button({ className = "", variant = "primary", ...props }: Props) {
  return (
    <button
      className={`rounded-lg px-4 py-2 font-medium transition-colors focus-visible:ring-2 focus-visible:ring-primary/60 focus-visible:ring-offset-2 focus-visible:ring-offset-bg focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-60 ${VARIANTS[variant]} ${className}`}
      {...props}
    />
  );
}
