const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' })

/** 1234.5 -> "₹1,234.50" (Indian digit grouping: 1,00,000.00). */
export function formatCurrency(amount) {
  return inr.format(Number(amount))
}

const dateTime = new Intl.DateTimeFormat('en-IN', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'Asia/Kolkata',
})

/** API timestamps are UTC ISO strings ("...Z"); always show them in IST. */
export function formatDateTime(isoString) {
  return dateTime.format(new Date(isoString))
}
