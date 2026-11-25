import type { Account } from "@/lib/api";

// What an account adds to net worth. A credit card's stored balance is the amount owed (the form labels it
// "Amount owed"), so it counts as negative, and an overpaid card, stored below zero, counts as money you have.
// Every other account counts as it stands, so an overdrawn bank account is negative too.
// Must agree with BalanceEffects.StoredChange in the backend, which applies transactions the same way round.
// `0 - x` rather than `-x`: a card at zero would give -0, which formats as "-$0.00".
export function netBalance(account: Pick<Account, "type" | "balance">): number {
  return account.type === "CreditCard" ? 0 - account.balance : account.balance;
}
