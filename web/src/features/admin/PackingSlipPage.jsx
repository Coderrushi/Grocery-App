import { useEffect } from 'react'
import { Link, useParams } from 'react-router'
import { config } from '../../app/config'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Spinner } from '../../shared/ui/Spinner'
import { formatCurrency, formatDateTime } from '../../shared/utils/format'
import { STATUS } from '../orders/orderStatusInfo'
import { useAdminOrder } from '../orders/ordersApi'

/**
 * One A4/A5 sheet that goes in the bag: a tick list for the packer and the customer's bill.
 * Rendered outside the store layout (no header or navigation) so it prints cleanly.
 */
export function PackingSlipPage() {
  const id = Number(useParams().id)
  const { data: detail, isPending, error } = useAdminOrder(id)

  useEffect(() => {
    if (detail) document.title = `${detail.order.orderNumber} packing slip | ${config.storeName}`
  }, [detail])

  if (isPending) return <Spinner />
  if (error) return <ErrorAlert error={error} title="Couldn't load this order" />

  const { order, customer } = detail
  const address = order.deliveryAddress
  const units = order.items.reduce((sum, i) => sum + i.quantity, 0)

  return (
    <main className="bg-surface text-ink mx-auto max-w-2xl p-6 print:max-w-none print:p-0">
      <div className="mb-6 flex items-center justify-between gap-3 print:hidden">
        <Link to={`/admin/orders/${order.id}`} className="text-brand-700 text-sm font-semibold">
          ← Back to order
        </Link>
        <Button onClick={() => window.print()}>🖨️ Print</Button>
      </div>

      <header className="border-ink flex items-start justify-between gap-4 border-b-2 pb-3">
        <div>
          <p className="text-xl font-bold">{config.storeName}</p>
          <p className="text-ink-muted text-sm">Packing slip &amp; bill</p>
        </div>
        <div className="text-right">
          <p className="text-xl font-bold">{order.orderNumber}</p>
          <p className="text-ink-muted text-sm">{formatDateTime(order.placedAtUtc)}</p>
          {order.status === 'Cancelled' && <p className="text-danger-700 font-bold">CANCELLED</p>}
        </div>
      </header>

      <section className="grid gap-4 border-b py-3 text-sm sm:grid-cols-2 print:grid-cols-2">
        <div>
          <p className="text-ink-muted text-xs font-semibold uppercase">Deliver to</p>
          <p className="font-semibold">{address.recipientName}</p>
          <p>
            {address.line1}
            {address.line2 && `, ${address.line2}`}
          </p>
          {address.landmark && <p>Near {address.landmark}</p>}
          <p>
            {address.city}, {address.state} {address.pincode}
          </p>
          <p className="font-semibold">📞 {address.phoneNumber}</p>
        </div>
        <div>
          <p className="text-ink-muted text-xs font-semibold uppercase">Customer</p>
          <p>{customer.fullName}</p>
          {order.deliveryInstructions && (
            <p className="border-ink mt-2 rounded border p-2">
              <span className="font-semibold">Note: </span>
              {order.deliveryInstructions}
            </p>
          )}
        </div>
      </section>

      <table className="mt-3 w-full text-left text-sm">
        <thead className="border-b">
          <tr>
            <th className="w-8 py-1" aria-label="Packed" />
            <th className="py-1 font-semibold">Item</th>
            <th className="py-1 text-right font-semibold">Qty</th>
            <th className="py-1 text-right font-semibold">Price</th>
            <th className="py-1 text-right font-semibold">Amount</th>
          </tr>
        </thead>
        <tbody className="divide-y tabular-nums">
          {order.items.map((item) => (
            <tr key={item.productId}>
              <td className="py-1.5 align-top">
                <span aria-hidden="true" className="border-ink inline-block h-4 w-4 rounded-sm border" />
              </td>
              <td className="py-1.5">
                {item.name} <span className="text-ink-muted">{item.unitLabel}</span>
                {item.brand && <span className="text-ink-muted block text-xs">{item.brand}</span>}
              </td>
              <td className="py-1.5 text-right font-semibold">{item.quantity}</td>
              <td className="py-1.5 text-right">{formatCurrency(item.unitPrice)}</td>
              <td className="py-1.5 text-right">{formatCurrency(item.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <dl className="mt-3 ml-auto w-64 space-y-1 border-t pt-2 text-sm tabular-nums">
        <div className="flex justify-between">
          <dt>Items ({units})</dt>
          <dd>{formatCurrency(order.subtotal)}</dd>
        </div>
        <div className="flex justify-between">
          <dt>Delivery</dt>
          <dd>{Number(order.deliveryFee) === 0 ? 'Free' : formatCurrency(order.deliveryFee)}</dd>
        </div>
        <div className="flex justify-between border-t pt-1 text-base font-bold">
          <dt>Total</dt>
          <dd>{formatCurrency(order.total)}</dd>
        </div>
        <div className="text-ink-muted flex justify-between">
          <dt>Paid</dt>
          <dd>{order.paymentStatus === 'Refunded' ? 'Refunded to wallet' : 'From wallet, nothing to collect'}</dd>
        </div>
        {Number(order.savings) > 0 && (
          <p className="pt-1 font-semibold">You saved {formatCurrency(order.savings)} on MRP!</p>
        )}
      </dl>

      <p className="text-ink-muted mt-6 text-center text-xs">
        Thank you for shopping with {config.storeName}. Status when printed: {STATUS[order.status].short}.
      </p>
    </main>
  )
}
