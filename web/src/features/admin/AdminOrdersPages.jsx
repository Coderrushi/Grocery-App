import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { Alert, ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { Spinner } from '../../shared/ui/Spinner'
import { formatCurrency, formatDateTime } from '../../shared/utils/format'
import { OrderBody } from '../orders/MyOrdersPages'
import { OrderProgress, OrderStatusBadge } from '../orders/orderStatus'
import { STATUS } from '../orders/orderStatusInfo'
import { useAdminOrder, useAdminOrders, useChangeOrderStatus } from '../orders/ordersApi'

const TABS = [
  { status: 'Placed', label: 'New' },
  { status: 'Confirmed', label: 'Confirmed' },
  { status: 'Packed', label: 'Packed' },
  { status: 'OutForDelivery', label: 'Out for delivery' },
  { status: 'Delivered', label: 'Delivered' },
  { status: 'Cancelled', label: 'Cancelled' },
  { status: '', label: 'All' },
]

export function AdminOrdersPage() {
  // The tab lives in the URL so the back button returns to the same list.
  const [params, setParams] = useSearchParams()
  const status = params.get('status') ?? 'Placed'
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const { data, isPending, isFetching, error } = useAdminOrders({ status, search, page })

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold">Orders</h1>

      <nav aria-label="Order status" className="-mx-4 overflow-x-auto px-4">
        <ul className="flex w-max gap-2">
          {TABS.map((tab) => (
            <li key={tab.label}>
              <button
                type="button"
                aria-pressed={status === tab.status}
                onClick={() => {
                  setParams(tab.status === 'Placed' ? {} : { status: tab.status })
                  setPage(1)
                }}
                className="border-line aria-pressed:border-brand-600 aria-pressed:bg-brand-600 h-9 rounded-full border px-3 text-sm font-semibold whitespace-nowrap aria-pressed:text-white"
              >
                {tab.label}
              </button>
            </li>
          ))}
        </ul>
      </nav>

      <form
        role="search"
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault()
          setSearch(searchInput.trim())
          setPage(1)
        }}
      >
        <input
          type="search"
          aria-label="Search orders"
          placeholder="Order number, customer name or phone"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          className="border-line bg-surface focus:border-brand-600 h-10 w-full max-w-sm rounded-lg border px-3 text-sm focus:outline-none"
        />
        <Button type="submit" size="sm" variant="secondary" className="h-10">
          Search
        </Button>
      </form>

      {isPending && <Spinner />}
      {error && <ErrorAlert error={error} title="Couldn't load orders" />}
      {data && (
        <>
          <div className="rounded-card border-line bg-surface overflow-x-auto border">
            <table className={`w-full text-left text-sm ${isFetching ? 'opacity-70' : ''}`}>
              <thead className="bg-canvas text-ink-muted">
                <tr>
                  <th className="px-4 py-3 font-semibold">Order</th>
                  <th className="px-4 py-3 font-semibold">Placed</th>
                  <th className="px-4 py-3 font-semibold">Customer</th>
                  <th className="px-4 py-3 font-semibold">PIN</th>
                  <th className="px-4 py-3 text-right font-semibold">Total</th>
                  <th className="px-4 py-3 font-semibold">Status</th>
                </tr>
              </thead>
              <tbody className="divide-line divide-y">
                {data.items.map((o) => (
                  <tr key={o.id} className="hover:bg-canvas">
                    <td className="px-4 py-3">
                      <Link to={`/admin/orders/${o.id}`} className="text-brand-700 font-semibold hover:underline">
                        {o.orderNumber}
                      </Link>
                      <span className="text-ink-muted block text-xs">{o.itemCount} items</span>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">{formatDateTime(o.placedAtUtc)}</td>
                    <td className="px-4 py-3">
                      {o.customerName}
                      <span className="text-ink-muted block text-xs">{o.customerPhone}</span>
                    </td>
                    <td className="px-4 py-3">{o.pincode}</td>
                    <td className="px-4 py-3 text-right font-semibold">{formatCurrency(o.total)}</td>
                    <td className="px-4 py-3">
                      <OrderStatusBadge status={o.status} />
                    </td>
                  </tr>
                ))}
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={6} className="text-ink-muted px-4 py-8 text-center">
                      No orders here.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between text-sm">
            <span className="text-ink-muted">{data.totalCount} orders · oldest first</span>
            <div className="flex items-center gap-2">
              <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                Previous
              </Button>
              <span>
                Page {data.page} of {Math.max(1, data.totalPages)}
              </span>
              <Button
                size="sm"
                variant="secondary"
                disabled={page >= data.totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

const NEXT_ACTION_LABELS = {
  Confirmed: 'Confirm order',
  Packed: 'Mark as packed',
  OutForDelivery: 'Send out for delivery',
  Delivered: 'Mark as delivered',
}

function StatusActions({ detail }) {
  const change = useChangeOrderStatus(detail.order.id)
  const [cancelling, setCancelling] = useState(false)
  const [reason, setReason] = useState('')
  const forward = detail.allowedNextStatuses.filter((s) => s !== 'Cancelled')
  const canCancel = detail.allowedNextStatuses.includes('Cancelled')

  if (detail.allowedNextStatuses.length === 0) return null

  const move = (status, note = null) => change.mutate({ status, note, version: detail.version })

  return (
    <Card title="Next step">
      <div className="space-y-3">
        {change.error?.status === 409 ? (
          <Alert tone="error" title="Someone else updated this order">
            The latest status has been loaded. Check it before continuing.
          </Alert>
        ) : (
          <ErrorAlert error={change.error} title="Couldn't update the order" />
        )}
        {forward.map((status) => (
          <Button key={status} className="w-full" size="lg" disabled={change.isPending} onClick={() => move(status)}>
            {STATUS[status].icon} {NEXT_ACTION_LABELS[status] ?? status}
          </Button>
        ))}
        {canCancel && !cancelling && (
          <Button variant="ghost" className="text-danger-700 w-full" onClick={() => setCancelling(true)}>
            Cancel order…
          </Button>
        )}
        {cancelling && (
          <div className="border-danger-600/30 bg-danger-50 space-y-2 rounded-lg border p-3">
            <label htmlFor="cancel-reason" className="text-sm font-semibold">
              Reason (the customer will see this)
            </label>
            <input
              id="cancel-reason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              maxLength={200}
              placeholder="e.g. Item out of stock, customer unreachable"
              className="border-line bg-surface h-10 w-full rounded-lg border px-3 text-sm"
            />
            <p className="text-ink-muted text-xs">
              {formatCurrency(detail.order.total)} will be refunded to the customer's wallet and the items returned to
              stock.
            </p>
            <div className="flex gap-2">
              <Button
                variant="danger"
                size="sm"
                disabled={!reason.trim() || change.isPending}
                onClick={() => move('Cancelled', reason.trim())}
              >
                Cancel and refund
              </Button>
              <Button variant="secondary" size="sm" onClick={() => setCancelling(false)}>
                Keep order
              </Button>
            </div>
          </div>
        )}
      </div>
    </Card>
  )
}

export function AdminOrderDetailPage() {
  const id = Number(useParams().id)
  const { data: detail, isPending, error } = useAdminOrder(id)

  if (isPending) return <Spinner />
  if (error) return <ErrorAlert error={error} title="Couldn't load this order" />

  const { order, customer } = detail
  return (
    <div className="space-y-6">
      <Link to="/admin/orders" className="text-brand-700 text-sm font-semibold">
        ← Orders
      </Link>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-2xl font-bold">{order.orderNumber}</h1>
          <p className="text-ink-muted text-sm">Placed {formatDateTime(order.placedAtUtc)}</p>
        </div>
        <OrderStatusBadge status={order.status} />
      </div>
      {order.status !== 'Cancelled' && (
        <Card>
          <OrderProgress status={order.status} />
        </Card>
      )}
      {order.status === 'Cancelled' && (
        <Alert tone="error" title="Cancelled">
          {order.cancellationReason}. Refunded {formatCurrency(order.total)} to the wallet.
        </Alert>
      )}
      <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
        <OrderBody order={order} />
        <div className="space-y-6">
          <StatusActions detail={detail} />
          <Card title="Customer">
            <p className="font-semibold">{customer.fullName}</p>
            {customer.phoneNumber && (
              <a href={`tel:+91${customer.phoneNumber}`} className="text-brand-700 block text-sm font-semibold">
                📞 {customer.phoneNumber}
              </a>
            )}
            <p className="text-ink-muted text-sm">{customer.email}</p>
            <Link to={`/admin/customers/${customer.id}`} className="text-brand-700 mt-2 inline-block text-sm">
              View wallet →
            </Link>
          </Card>
        </div>
      </div>
    </div>
  )
}
