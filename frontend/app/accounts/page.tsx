import type { Metadata } from "next";
import { BankAccounts } from "./BankAccounts";

export const metadata: Metadata = { title: "Accounts · Zhadina Budget" };

export default function AccountsPage() {
  return <BankAccounts />;
}
