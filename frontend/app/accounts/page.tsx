import type { Metadata } from "next";
import { Accounts } from "./Accounts";

export const metadata: Metadata = { title: "Accounts · Zhadina Budget" };

export default function AccountsPage() {
  return <Accounts />;
}
