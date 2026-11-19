import type { Account } from "@/lib/api";

// What an account adds to net worth. A credit card's balance is money owed, so it always counts as negative,
// whichever sign was typed in (the form labels it "Amount owed"). Every other account counts as it stands,
// so an overdrawn bank account is negative too.
// `0 - x` rather than `-x`: a card at zero would give -0, which formats as "-$0.00".
export function netBalance(account: Pick<Account, "type" | "balance">): number {
  return account.type === "CreditCard" ? 0 - Math.abs(account.balance) : account.balance;
}
