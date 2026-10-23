import type { Metadata } from "next";
import { AuthCard } from "@/components/AuthCard";
import { AuthForm } from "@/components/AuthForm";

export const metadata: Metadata = { title: "Log in · Zhadina Budget" };

export default function LoginPage() {
  return (
    <AuthCard title="Welcome back" subtitle="Log in to see your budget.">
      <AuthForm mode="login" />
    </AuthCard>
  );
}
