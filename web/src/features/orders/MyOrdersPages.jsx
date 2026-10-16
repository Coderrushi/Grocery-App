import { Link, useParams, useSearchParams } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { Alert, ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { Spinner } from '../../shared/ui/Spinner'
import { formatCurrency, formatDateTime } from '../../shared/utils/format'
import { NotFoundPage } from '../../pages/ErrorPages'
import { BillLines } from '../cart/BillLines'
import { OrderProgress, OrderStatusBadge } from './orderStatus'
import { STATUS } from './orderStatusInfo'
import { useCancelOrder, useMyOrders, useOrder } from './ordersApi'

export function MyOrdersPage() {
  const { data, isPending, error, hasNextPage, fetchNextPage, isFetchingNextPage } = useMyOrders()

  if (isPending) return <Spinner label="Loading your orders…" />
  if (error) return <ErrorAlert error={error} title="Couldn't load your orders" />

  const orders = data.pages.flatMap((p) => p.items)
  if (orders.length === 0) {
    return (
      <div className="py-10 text-center">
        <p className="text-ink-muted">You haven't placed any orders yet.</p>
        <Link to="/" className="text-brand-700 mt-2 inline-block font-semibold">
          Start shopping
        </Link>
      </div>
    )
  }

  return (
    <div>
      <ul className="space-y-3">
        {orders.map((o) => (
          <li key={o.id}>
            <Link
              to={`/account/orders/${o.id}`}
              className="rounded-card border-line bg-surface shadow-card hover:border-brand-600 block border p-4"
            >
              <div className="flex items-center justify-between gap-2">
                <span className="font-semibold">{o.orderNumber}</span>
                <OrderStatusBadge status={o.status} />
              </div>
              <p className="text-ink-muted mt-1 line-clamp-1 text-sm">{o.itemsPreview}</p>
              <div className="mt-2 flex justify-between text-sm">
                <span className="text-ink-muted">{formatDateTime(o.placedAtUtc)}</span>
                <span className="font-semibold">
                  {formatCurrency(o.total)} · {o.itemCount} item{o.itemCount === 1 ? '' : 's'}
                </span>
              </div>
            </Link>
          </li>
        ))}
      </ul>
      {hasNextPage && (
        <div className="mt-4 text-center">
          <Button variant="secondary" onClick={() => fetchNextPage()} disabled={isFetchingNextPage}>
            {isFetchingNextPage ? 'Loading…' : 'Show older orders'}
          </Button>
        </div>
      )}
    </div>
  )
}

/** Shared by the customer and admin order pages. */
export function OrderBody({ order }) {
  const a = order.deliveryAddress
  return (
    <div className="space-y-6">
      <Card title="Items">
        <ul className="divide-line divide-y text-sm">
          {order.items.map((item) => (
            <li key={item.productId} className="flex justify-between gap-3 py-2">
              <span>
                {item.name} <span className="text-ink-muted">({item.unitLabel})</span>
                <span className="text-ink-muted block text-xs">
                  {formatCurrency(item.unitPrice)} × {item.quantity}
                </span>
              </span>
              <span className="font-medium">{formatCurrency(item.lineTotal)}</span>
            </li>
          ))}
        </ul>
        <div className="mt-4">
          <BillLines
            totals={order}
            totalLabel={order.paymentStatus === 'Refunded' ? 'Paid (refunded)' : 'Paid from wallet'}
          />
        </div>
      </Card>
      <Card title="Delivery to">
        <p className="text-sm font-semibold">
          {a.recipientName} · {a.phoneNumber}
        </p>
        <p className="text-ink-muted text-sm">
          {[a.line1, a.line2, a.landmark && `Near ${a.landmark}`, `${a.city}, ${a.state} ${a.pincode}`]
            .filter(Boolean)
            .join(', ')}
        </p>
        {order.deliveryInstructions && <p className="mt-2 text-sm">📝 {order.deliveryInstructions}</p>}
      </Card>
      <Card title="Timeline">
        <ol className="space-y-3 text-sm">
          {order.timeline.map((t, i) => (
            <li key={i} className="flex gap-3">
              <span aria-hidden="true">{STATUS[t.toStatus]?.icon}</span>
              <div>
                <p className="font-medium">
                  {STATUS[t.toStatus]?.label ?? t.toStatus}
                  {t.changedBy && <span className="text-ink-muted font-normal"> · {t.changedBy}</span>}
                </p>
                <p className="text-ink-muted text-xs">{formatDateTime(t.changedAtUtc)}</p>
                {t.note && <p className="text-ink-muted text-xs">“{t.note}”</p>}
              </div>
            </li>
          ))}
        </ol>
      </Card>
    </div>
  )
}

export function OrderDetailPage() {
  const id = Number(useParams().id)
  const [params] = useSearchParams()
  const { data: order, isPending, error } = useOrder(id)
  const cancel = useCancelOrder(id)

  if (isPending) return <Spinner />
  if (error?.status === 404) return <NotFoundPage />
  if (error) return <ErrorAlert error={error} title="Couldn't load this order" />

  const onCancel = () => {
    if (window.confirm('Cancel this order? The full amount will be refunded to your wallet.')) cancel.mutate(null)
  }

  return (
    <div className="space-y-6">
      <Link to="/account/orders" className="text-brand-700 text-sm font-semibold">
        ← All orders
      </Link>
      {params.get('placed') && order.status === 'Placed' && (
        <Alert tone="success" title="🎉 Order placed!">
          Thank you! We've received order {order.orderNumber}. You'll see updates here as we prepare it.
        </Alert>
      )}
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-xl font-bold">{order.orderNumber}</h2>
          <p className="text-ink-muted text-sm">Placed {formatDateTime(order.placedAtUtc)}</p>
        </div>
        <OrderStatusBadge status={order.status} />
      </div>

      {order.status === 'Cancelled' ? (
        <Alert tone="error" title="This order was cancelled">
          {order.cancellationReason && <p>Reason: {order.cancellationReason}</p>}
          <p>{formatCurrency(order.total)} has been refunded to your wallet.</p>
        </Alert>
      ) : (
        <Card>
          <OrderProgress status={order.status} />
        </Card>
      )}

      <OrderBody order={order} />

      {order.canCancel && (
        <div>
          <ErrorAlert error={cancel.error} title="Couldn't cancel" />
          <Button variant="secondary" className="text-danger-700" onClick={onCancel} disabled={cancel.isPending}>
            {cancel.isPending ? 'Cancelling…' : 'Cancel order'}
          </Button>
          <p className="text-ink-muted mt-1 text-xs">You can cancel until the store confirms your order.</p>
        </div>
      )}
    </div>
  )
}
