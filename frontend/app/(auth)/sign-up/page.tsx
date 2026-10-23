import type { Metadata } from "next";
import { AuthCard } from "@/components/AuthCard";
import { AuthForm } from "@/components/AuthForm";

export const metadata: Metadata = { title: "Sign up · Zhadina Budget" };

export default function SignUpPage() {
  return (
    <AuthCard title="Create your account" subtitle="At least 8 characters, including a number.">
      <AuthForm mode="sign-up" />
    </AuthCard>
  );
}
