/** Labels, colours and icons for each order status. */
export const STATUS = {
  Placed: { label: 'Order placed', short: 'Placed', tone: 'bg-accent-100 text-accent-600', icon: '🧾' },
  Confirmed: { label: 'Confirmed by store', short: 'Confirmed', tone: 'bg-brand-100 text-brand-900', icon: '✅' },
  Packed: { label: 'Packed', short: 'Packed', tone: 'bg-brand-100 text-brand-900', icon: '📦' },
  OutForDelivery: { label: 'Out for delivery', short: 'Out for delivery', tone: 'bg-brand-600 text-white', icon: '🛵' },
  // Inverted pill: strong contrast in both themes and visually distinct from the in-progress steps.
  Delivered: { label: 'Delivered', short: 'Delivered', tone: 'bg-brand-900 text-surface', icon: '🏠' },
  Cancelled: { label: 'Cancelled', short: 'Cancelled', tone: 'bg-danger-50 text-danger-700', icon: '✖️' },
}
