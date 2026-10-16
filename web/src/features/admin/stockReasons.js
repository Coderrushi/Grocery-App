/** Stock-ledger reasons, shared by the stock panel and the stock report. */

// Reasons the shopkeeper can pick, and whether they add (+1) or remove (-1) stock.
export const REASONS = [
  { value: 'Restock', label: 'Restock (new delivery)', sign: 1 },
  { value: 'Returned', label: 'Customer return', sign: 1 },
  { value: 'Damaged', label: 'Damaged', sign: -1 },
  { value: 'Expired', label: 'Expired', sign: -1 },
  { value: 'Correction', label: 'Stock-count correction (+/−)', sign: 0 },
]

export const REASON_LABELS = {
  InitialStock: 'Opening stock',
  Sale: 'Sold (order)',
  OrderCancelled: 'Order cancelled (returned)',
  ...Object.fromEntries(REASONS.map((r) => [r.value, r.label])),
}
