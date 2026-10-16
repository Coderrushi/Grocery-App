/** How each wallet transaction type is shown, in the wallet history and the admin reports. */
export const TYPE_LABELS = {
  TopUp: { label: 'Money added', icon: '➕' },
  OrderPayment: { label: 'Order payment', icon: '🛍️' },
  OrderRefund: { label: 'Order refund', icon: '↩️' },
  AdminCredit: { label: 'Credit from store', icon: '🎁' },
  AdminDebit: { label: 'Adjustment by store', icon: '➖' },
}
