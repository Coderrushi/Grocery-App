/** One-line address for cards and order summaries. */
export function formatAddress(a) {
  return [a.line1, a.line2, a.landmark && `Near ${a.landmark}`, `${a.city}, ${a.state} ${a.pincode}`]
    .filter(Boolean)
    .join(', ')
}
