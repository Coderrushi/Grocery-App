const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' })

/** 1234.5 -> "₹1,234.50" (Indian digit grouping: 1,00,000.00). */
export function formatCurrency(amount) {
  return inr.format(Number(amount))
}

const inrCompact = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  notation: 'compact',
  maximumFractionDigits: 1,
})

/** For chart axes: 1500 -> "₹1.5K", 250000 -> "₹2.5L" (lakh). */
export function formatCompactCurrency(amount) {
  return inrCompact.format(Number(amount))
}

const shortDay = new Intl.DateTimeFormat('en-IN', { day: 'numeric', month: 'short', timeZone: 'UTC' })
const longDay = new Intl.DateTimeFormat('en-IN', {
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
})

/**
 * Report dates are calendar days ("2026-10-15"), not instants: format them as-is (UTC), never
 * shifted by a time zone. `long` adds the weekday and year.
 */
export function formatDay(isoDate, { long = false } = {}) {
  return (long ? longDay : shortDay).format(new Date(`${isoDate}T00:00:00Z`))
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
