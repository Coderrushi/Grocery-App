import { Link } from 'react-router'
import { Card } from '../../shared/ui/Card'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Spinner } from '../../shared/ui/Spinner'
import { formatCurrency, formatDateTime, formatDay } from '../../shared/utils/format'
import { OrderStatusBadge } from '../orders/orderStatus'
import { STATUS } from '../orders/orderStatusInfo'
import { DailySalesChart, StatTile } from './charts'
import { useDashboard } from './reportsApi'

function versus(today, yesterday, format = String) {
  if (today === yesterday) return `Same as yesterday (${format(yesterday)})`
  return `${today > yesterday ? 'Up' : 'Down'} from ${format(yesterday)} yesterday`
}

/** The admin home page: what needs doing now, and how today is going. */
export function AdminDashboardPage() {
  const { data, isPending, error } = useDashboard()

  if (isPending) return <Spinner />
  if (error) return <ErrorAlert error={error} title="Couldn't load the dashboard" />

  const { todaySales: today, yesterdaySales: yesterday } = data
  const weekTotal = data.last7Days.reduce((sum, d) => sum + Number(d.sales), 0)

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Dashboard</h1>
        <p className="text-ink-muted text-sm">{formatDay(data.today, { long: true })}</p>
      </div>

      <section aria-labelledby="queue-heading">
        <h2 id="queue-heading" className="sr-only">
          Orders to handle
        </h2>
        <ul className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {data.openOrders.map(({ status, count }) => (
            <li key={status}>
              <Link
                to={status === 'Placed' ? '/admin/orders' : `/admin/orders?status=${status}`}
                className="rounded-card border-line bg-surface shadow-card hover:border-brand-600 block border p-4"
              >
                <span className="text-ink-muted text-sm">
                  <span aria-hidden="true">{STATUS[status].icon}</span> {STATUS[status].short}
                </span>
                <span className="mt-1 block text-2xl font-bold">{count}</span>
              </Link>
            </li>
          ))}
        </ul>
      </section>

      <section aria-label="Today's sales" className="grid gap-3 sm:grid-cols-3">
        <StatTile
          label="Sales today"
          value={formatCurrency(today.sales)}
          detail={versus(Number(today.sales), Number(yesterday.sales), formatCurrency)}
        />
        <StatTile label="Orders today" value={today.orders} detail={versus(today.orders, yesterday.orders)} />
        <StatTile
          label="Average order"
          value={formatCurrency(today.averageOrderValue)}
          detail={
            data.cancelledToday > 0 ? `${data.cancelledToday} cancelled today (not counted)` : 'No cancellations today'
          }
        />
      </section>

      <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
        <Card
          title="Sales, last 7 days"
          description={`${formatCurrency(weekTotal)} in total`}
          actions={
            <Link to="/admin/reports" className="text-brand-700 text-sm font-semibold whitespace-nowrap">
              Reports →
            </Link>
          }
        >
          <DailySalesChart days={data.last7Days} />
        </Card>

        <Card
          title="Stock alerts"
          description={`${data.outOfStockCount} out of stock · ${data.lowStockCount} running low`}
        >
          {data.stockAlerts.length === 0 ? (
            <p className="text-ink-muted text-sm">Everything is well stocked.</p>
          ) : (
            <ul className="divide-line divide-y text-sm">
              {data.stockAlerts.map((p) => (
                <li key={p.productId} className="flex items-center justify-between gap-3 py-2">
                  <Link to={`/admin/products/${p.productId}`} className="min-w-0 truncate hover:underline">
                    {p.name} <span className="text-ink-muted">{p.unitLabel}</span>
                  </Link>
                  <span
                    className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold ${
                      p.stockQuantity === 0 ? 'bg-danger-50 text-danger-700' : 'bg-accent-100 text-accent-600'
                    }`}
                  >
                    {p.stockQuantity === 0 ? 'Out of stock' : `${p.stockQuantity} left`}
                  </span>
                </li>
              ))}
            </ul>
          )}
          <Link to="/admin/reports/inventory" className="text-brand-700 mt-3 inline-block text-sm font-semibold">
            Reorder list →
          </Link>
        </Card>
      </div>

      <Card
        title="Latest orders"
        actions={
          <Link to="/admin/orders?status=" className="text-brand-700 text-sm font-semibold whitespace-nowrap">
            All orders →
          </Link>
        }
      >
        {data.recentOrders.length === 0 ? (
          <p className="text-ink-muted text-sm">No orders yet.</p>
        ) : (
          <ul className="divide-line divide-y text-sm">
            {data.recentOrders.map((o) => (
              <li key={o.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                <span>
                  <Link to={`/admin/orders/${o.id}`} className="text-brand-700 font-semibold hover:underline">
                    {o.orderNumber}
                  </Link>{' '}
                  <span className="text-ink-muted">
                    {o.customerName} · {formatDateTime(o.placedAtUtc)}
                  </span>
                </span>
                <span className="flex items-center gap-3">
                  <span className="font-semibold">{formatCurrency(o.total)}</span>
                  <OrderStatusBadge status={o.status} />
                </span>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  )
}
