import type { ReactNode } from "react";

export function AuthCard({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  return (
    <main className="flex flex-1 items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-8 shadow-xl shadow-black/30">
        <p className="mb-6 text-sm font-semibold tracking-wide text-primary uppercase">Zhadina Budget</p>
        <h1 className="text-2xl font-semibold">{title}</h1>
        <p className="mt-1 mb-6 text-sm text-muted">{subtitle}</p>
        {children}
      </div>
    </main>
  );
}
