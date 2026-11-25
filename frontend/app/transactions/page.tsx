import type { Metadata } from "next";
import { Transactions } from "./Transactions";

export const metadata: Metadata = { title: "Transactions · Zhadina Budget" };

export default function TransactionsPage() {
  return <Transactions />;
}
