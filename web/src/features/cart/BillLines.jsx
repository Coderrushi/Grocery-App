import { formatCurrency } from '../../shared/utils/format'

/**
 * The bill breakdown, shared by the cart, checkout and order pages so they always look and read alike.
 * `totals`: { subtotal, mrpTotal, savings, deliveryFee, total }
 */
export function BillLines({ totals, totalLabel = 'To pay' }) {
  return (
    <>
      <dl className="space-y-2 text-sm">
        <div className="flex justify-between">
          <dt>Item total</dt>
          <dd>
            {totals.savings > 0 && (
              <span className="text-ink-muted mr-2 line-through">{formatCurrency(totals.mrpTotal)}</span>
            )}
            {formatCurrency(totals.subtotal)}
          </dd>
        </div>
        <div className="flex justify-between">
          <dt>Delivery fee</dt>
          <dd>
            {totals.deliveryFee === 0 ? (
              <span className="text-brand-700 font-semibold">FREE</span>
            ) : (
              formatCurrency(totals.deliveryFee)
            )}
          </dd>
        </div>
        <div className="border-line flex justify-between border-t pt-2 text-base font-bold">
          <dt>{totalLabel}</dt>
          <dd>{formatCurrency(totals.total)}</dd>
        </div>
      </dl>
      {totals.savings > 0 && (
        <p className="bg-brand-50 text-brand-900 mt-3 rounded-lg px-3 py-2 text-sm font-semibold">
          You save {formatCurrency(totals.savings)} on this order
        </p>
      )}
    </>
  )
}
