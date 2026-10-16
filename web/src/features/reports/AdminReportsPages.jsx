import { useState } from 'react'
import { Link, NavLink, Outlet, useOutletContext, useSearchParams } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { Spinner } from '../../shared/ui/Spinner'
import { formatCurrency, formatDateTime, formatDay } from '../../shared/utils/format'
import { REASON_LABELS } from '../admin/stockReasons'
import { TYPE_LABELS } from '../wallet/transactionTypes'
import { DailySalesChart, RankedBars, StatTile } from './charts'
import { DEFAULT_PRESET, PRESETS, matchPreset, presetPeriod } from './period'
import { csvUrl, useCustomerReport, useInventoryReport, useSalesReport } from './reportsApi'

const MAX_DAYS = 366

const TABS = [
  { to: '/admin/reports', label: 'Sales', end: true },
  { to: '/admin/reports/inventory', label: 'Stock' },
  { to: '/admin/reports/customers', label: 'Customers & wallet' },
]

/** The period lives in the URL (?from=&to=), so a report can be bookmarked or shared with the same dates. */
function usePeriod() {
  const [params, setParams] = useSearchParams()
  const fallback = presetPeriod(DEFAULT_PRESET)
  const period = { from: params.get('from') ?? fallback.from, to: params.get('to') ?? fallback.to }
  return [period, (next) => setParams({ from: next.from, to: next.to })]
}

function PeriodPicker({ period, onChange }) {
  const preset = matchPreset(period)
  const [custom, setCustom] = useState(!preset)
  const [draft, setDraft] = useState(period)
  const days = (new Date(draft.to) - new Date(draft.from)) / 86_400_000 + 1
  const problem =
    !draft.from || !draft.to
      ? 'Choose both dates.'
      : days < 1
        ? 'The end date must be on or after the start date.'
        : days > MAX_DAYS
          ? `Choose at most ${MAX_DAYS} days.`
          : null

  return (
    <div className="flex flex-wrap items-end gap-3">
      <label className="text-sm">
        <span className="text-ink-muted mb-1 block font-semibold">Period</span>
        <select
          value={custom ? 'custom' : preset}
          onChange={(e) => {
            if (e.target.value === 'custom') {
              setDraft(period)
              setCustom(true)
            } else {
              setCustom(false)
              onChange(presetPeriod(e.target.value))
            }
          }}
          className="border-line bg-surface h-10 rounded-lg border px-3 text-sm"
        >
          {PRESETS.map((p) => (
            <option key={p.id} value={p.id}>
              {p.label}
            </option>
          ))}
          <option value="custom">Custom dates…</option>
        </select>
      </label>
      {custom && (
        <form
          className="flex flex-wrap items-end gap-2"
          onSubmit={(e) => {
            e.preventDefault()
            if (!problem) onChange(draft)
          }}
        >
          {['from', 'to'].map((key) => (
            <label key={key} className="text-sm">
              <span className="text-ink-muted mb-1 block font-semibold">{key === 'from' ? 'From' : 'To'}</span>
              <input
                type="date"
                value={draft[key]}
                onChange={(e) => setDraft((d) => ({ ...d, [key]: e.target.value }))}
                className="border-line bg-surface h-10 rounded-lg border px-3 text-sm"
              />
            </label>
          ))}
          <Button type="submit" size="sm" className="h-10" disabled={Boolean(problem)}>
            Show
          </Button>
          {problem && <p className="text-danger-700 w-full text-sm">{problem}</p>}
        </form>
      )}
      <p className="text-ink-muted text-sm sm:ml-auto">
        {formatDay(period.from, { long: true })} – {formatDay(period.to, { long: true })}
      </p>
    </div>
  )
}

export function AdminReportsLayout() {
  const [period, setPeriod] = usePeriod()
  const [params] = useSearchParams()

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Reports</h1>
      <nav aria-label="Reports" className="-mx-4 overflow-x-auto px-4">
        <ul className="flex w-max gap-2">
          {TABS.map((tab) => (
            <li key={tab.to}>
              <NavLink
                to={{ pathname: tab.to, search: params.toString() }}
                end={tab.end}
                className={({ isActive }) =>
                  `border-line block h-9 rounded-full border px-3 text-sm leading-9 font-semibold whitespace-nowrap ${
                    isActive ? 'border-brand-600 bg-brand-600 text-white' : ''
                  }`
                }
              >
                {tab.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
      {/* key: a new period (e.g. browser back) resets the custom-date draft */}
      <PeriodPicker key={`${period.from}_${period.to}`} period={period} onChange={setPeriod} />
      <Outlet context={period} />
    </div>
  )
}

function Downloads({ links }) {
  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      <span className="text-ink-muted font-semibold">Download CSV:</span>
      {links.map(({ label, href }) => (
        <a
          key={href}
          href={href}
          download
          className="border-line hover:bg-canvas rounded-full border px-3 py-1 font-semibold"
        >
          ⬇ {label}
        </a>
      ))}
    </div>
  )
}

/** Loading, error and "dim while refetching" handling shared by the three reports. */
function ReportFrame({ query, title, children }) {
  if (query.isPending) return <Spinner />
  if (query.error) return <ErrorAlert error={query.error} title={`Couldn't load the ${title}`} />
  return (
    <div className={`space-y-6 transition-opacity ${query.isPlaceholderData ? 'opacity-60' : ''}`}>
      {children(query.data)}
    </div>
  )
}

function Table({ columns, rows, empty }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead className="text-ink-muted">
          <tr>
            {columns.map((c) => (
              <th key={c.label} className={`py-2 pr-3 font-semibold ${c.numeric ? 'text-right' : ''}`}>
                {c.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-line divide-y">
          {rows.map((row) => (
            <tr key={row.key}>
              {columns.map((c) => (
                <td key={c.label} className={`py-2 pr-3 ${c.numeric ? 'text-right tabular-nums' : ''}`}>
                  {c.render(row)}
                </td>
              ))}
            </tr>
          ))}
          {rows.length === 0 && (
            <tr>
              <td colSpan={columns.length} className="text-ink-muted py-6 text-center">
                {empty}
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  )
}

// ---------- Sales ----------

export function SalesReportPage() {
  const period = useOutletContext()
  const query = useSalesReport(period)
  const [rankBy, setRankBy] = useState('revenue')

  return (
    <ReportFrame query={query} title="sales report">
      {(report) => {
        const { totals } = report
        const top = rankBy === 'revenue' ? report.topByRevenue : report.topByQuantity
        return (
          <>
            <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
              <StatTile
                label="Sales"
                value={formatCurrency(totals.sales)}
                detail={`${formatCurrency(totals.deliveredSales)} delivered · ${formatCurrency(totals.inProgressSales)} in progress`}
              />
              <StatTile
                label="Orders"
                value={totals.orders}
                detail={`${formatCurrency(totals.deliveryFees)} in delivery fees`}
              />
              <StatTile
                label="Average order"
                value={formatCurrency(totals.averageOrderValue)}
                detail={`Customers saved ${formatCurrency(totals.customerSavings)} on MRP`}
              />
              <StatTile
                label="Cancelled"
                value={totals.cancelledOrders}
                detail={`${formatCurrency(totals.cancelledValue)} refunded to wallets`}
              />
            </div>

            <Card title="Sales by day" description="Orders placed each day (Indian time), cancelled orders excluded">
              <DailySalesChart days={report.days} />
            </Card>

            <div className="grid gap-6 lg:grid-cols-2">
              <Card
                title="Top products"
                actions={
                  <div role="group" aria-label="Rank by" className="flex gap-1">
                    {[
                      ['revenue', 'Sales'],
                      ['quantity', 'Units'],
                    ].map(([id, label]) => (
                      <button
                        key={id}
                        type="button"
                        aria-pressed={rankBy === id}
                        onClick={() => setRankBy(id)}
                        className="border-line aria-pressed:border-brand-600 aria-pressed:bg-brand-600 h-8 rounded-full border px-3 text-xs font-semibold aria-pressed:text-white"
                      >
                        {label}
                      </button>
                    ))}
                  </div>
                }
              >
                <RankedBars
                  rows={top.map((p) => ({
                    key: p.productId,
                    label: p.name,
                    detail: p.unitLabel,
                    value: rankBy === 'revenue' ? p.sales : p.quantity,
                    valueLabel: rankBy === 'revenue' ? formatCurrency(p.sales) : `${p.quantity} sold`,
                  }))}
                />
              </Card>
              <Card title="Sales by category" description="Item value, delivery fees excluded">
                <RankedBars
                  rows={report.categories.map((c) => ({
                    key: c.categoryId,
                    label: c.name,
                    detail: `${c.quantity} units`,
                    value: c.sales,
                    valueLabel: formatCurrency(c.sales),
                  }))}
                />
              </Card>
            </div>

            <Downloads
              links={[
                { label: 'Daily sales', href: csvUrl('sales/daily.csv', period) },
                { label: 'Sales by product', href: csvUrl('sales/products.csv', period) },
                { label: 'All orders', href: csvUrl('orders.csv', period) },
              ]}
            />
          </>
        )
      }}
    </ReportFrame>
  )
}

// ---------- Stock ----------

export function InventoryReportPage() {
  const period = useOutletContext()
  const query = useInventoryReport(period)

  return (
    <ReportFrame query={query} title="stock report">
      {(report) => (
        <>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <StatTile label="Products on sale" value={report.activeProducts} />
            <StatTile label="Out of stock" value={report.outOfStockCount} />
            <StatTile label="Running low" value={report.lowStockCount} />
            <StatTile label="Stock value" value={formatCurrency(report.stockValueAtPrice)} detail="At selling price" />
          </div>

          <Card
            title="Reorder list"
            description="At or below their low-stock level now, emptiest first. Days left assumes the last 30 days' sales rate."
          >
            <Table
              empty="Nothing needs reordering."
              rows={report.reorder.map((r) => ({ ...r, key: r.productId }))}
              columns={[
                {
                  label: 'Product',
                  render: (r) => (
                    <Link to={`/admin/products/${r.productId}`} className="hover:underline">
                      <span className="font-semibold">{r.name}</span>{' '}
                      <span className="text-ink-muted">{r.unitLabel}</span>
                      <span className="text-ink-muted block text-xs">{r.category}</span>
                    </Link>
                  ),
                },
                {
                  label: 'In stock',
                  numeric: true,
                  render: (r) => (
                    <span className={r.stockQuantity === 0 ? 'text-danger-700 font-semibold' : ''}>
                      {r.stockQuantity} / {r.lowStockThreshold}
                    </span>
                  ),
                },
                { label: 'Sold (30 days)', numeric: true, render: (r) => r.soldLast30Days },
                { label: 'Days left', numeric: true, render: (r) => (r.daysOfCover === null ? '—' : r.daysOfCover) },
              ]}
            />
            <div className="mt-4">
              <Downloads links={[{ label: 'Reorder list', href: csvUrl('inventory/reorder.csv') }]} />
            </div>
          </Card>

          <div className="grid gap-6 lg:grid-cols-2">
            <Card title="Stock movements" description="Units in (+) and out (−) during the period, by reason">
              <Table
                empty="No stock movements in this period."
                rows={report.movements.map((m) => ({ ...m, key: m.reason }))}
                columns={[
                  { label: 'Reason', render: (m) => REASON_LABELS[m.reason] ?? m.reason },
                  { label: 'Entries', numeric: true, render: (m) => m.entries },
                  { label: 'Units', numeric: true, render: (m) => (m.units > 0 ? `+${m.units}` : m.units) },
                ]}
              />
            </Card>
            <Card
              title="Losses"
              description={`Damaged or expired: ${formatCurrency(report.lossValue)} at selling price`}
            >
              <Table
                empty="No damaged or expired stock in this period. 🎉"
                rows={report.losses.map((l) => ({ ...l, key: l.productId }))}
                columns={[
                  { label: 'Product', render: (l) => `${l.name} (${l.unitLabel})` },
                  { label: 'Damaged', numeric: true, render: (l) => l.damaged },
                  { label: 'Expired', numeric: true, render: (l) => l.expired },
                  { label: 'Value', numeric: true, render: (l) => formatCurrency(l.value) },
                ]}
              />
            </Card>
          </div>

          <Downloads
            links={[{ label: 'Stock ledger for the period', href: csvUrl('inventory/movements.csv', period) }]}
          />
        </>
      )}
    </ReportFrame>
  )
}

// ---------- Customers & wallet ----------

export function CustomerReportPage() {
  const period = useOutletContext()
  const query = useCustomerReport(period)

  return (
    <ReportFrame query={query} title="customer report">
      {(report) => (
        <>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <StatTile label="New sign-ups" value={report.newCustomers} />
            <StatTile label="Customers who ordered" value={report.orderingCustomers} />
            <StatTile label="First-time buyers" value={report.firstTimeCustomers} />
            <StatTile
              label="Returning buyers"
              value={report.returningCustomers}
              detail="Had ordered before this period"
            />
          </div>

          <Card title="Top customers" description="By amount spent in the period (cancelled orders excluded)">
            <Table
              empty="No orders in this period."
              rows={report.topCustomers.map((c) => ({ ...c, key: c.userId }))}
              columns={[
                {
                  label: 'Customer',
                  render: (c) => (
                    <Link to={`/admin/customers/${c.userId}`} className="hover:underline">
                      <span className="font-semibold">{c.fullName}</span>
                      {c.phoneNumber && <span className="text-ink-muted block text-xs">{c.phoneNumber}</span>}
                    </Link>
                  ),
                },
                { label: 'Orders', numeric: true, render: (c) => c.orders },
                { label: 'Spent', numeric: true, render: (c) => formatCurrency(c.spent) },
                { label: 'Last order', render: (c) => formatDateTime(c.lastOrderAtUtc) },
              ]}
            />
            <div className="mt-4">
              <Downloads links={[{ label: 'All customers by spend', href: csvUrl('customers.csv', period) }]} />
            </div>
          </Card>

          <Card
            title="Wallets"
            description="Money in customer wallets is owed by the shop: it must stay available for orders and refunds."
          >
            <div className="grid gap-6 sm:grid-cols-[14rem_1fr]">
              <div>
                <p className="text-ink-muted text-sm">Held in wallets now</p>
                <p className="text-2xl font-bold">{formatCurrency(report.walletBalanceTotal)}</p>
                <p className="text-ink-muted text-xs">{report.customersWithBalance} customers have a balance</p>
              </div>
              <Table
                empty="No wallet activity in this period."
                rows={report.walletFlows.map((f) => ({ ...f, key: f.type }))}
                columns={[
                  { label: 'In the period', render: (f) => TYPE_LABELS[f.type]?.label ?? f.type },
                  { label: 'Count', numeric: true, render: (f) => f.count },
                  {
                    label: 'Amount',
                    numeric: true,
                    render: (f) => `${Number(f.amount) > 0 ? '+' : ''}${formatCurrency(f.amount)}`,
                  },
                ]}
              />
            </div>
          </Card>
        </>
      )}
    </ReportFrame>
  )
}
