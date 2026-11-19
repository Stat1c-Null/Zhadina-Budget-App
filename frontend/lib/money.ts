// Nothing stores a currency yet, so every amount is shown as US dollars.
// The locale is fixed because some others would show USD as "US$".
export const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
