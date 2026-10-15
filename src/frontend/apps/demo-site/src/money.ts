// Money as the casino shows it (pt-BR, as the game does): "2990.5" → "R$ 2.990,50". Formatted from the decimal string of
// the wallet, never through a binary float.

const currencySymbols: Readonly<Record<string, string>> = { BRL: "R$" };

export function formatMoney(amount: string, currency: string): string {
  const negative = amount.startsWith("-");
  const [units = "0", fraction = ""] = (negative ? amount.slice(1) : amount).split(".");
  const grouped = units.replace(/\B(?=(\d{3})+(?!\d))/gu, ".");
  const symbol = currencySymbols[currency] ?? currency;
  return `${negative ? "-" : ""}${symbol} ${grouped},${fraction.padEnd(2, "0").slice(0, 2)}`;
}
