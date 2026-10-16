import { useEffect, useRef, useState } from 'react'
import { formatCompactCurrency, formatCurrency, formatDay } from '../../shared/utils/format'
import { niceTicks } from './chartScale'

function useWidth(ref, fallback) {
  const [width, setWidth] = useState(fallback)
  useEffect(() => {
    const el = ref.current
    if (!el || typeof ResizeObserver === 'undefined') return undefined // jsdom (tests) keeps the fallback
    const observer = new ResizeObserver(([entry]) => setWidth(Math.round(entry.contentRect.width)))
    observer.observe(el)
    return () => observer.disconnect()
  }, [ref])
  return width
}

/** A column with a 4px rounded top and a square foot on the baseline. */
function columnPath(x, top, width, bottom) {
  const r = Math.min(4, width / 2, bottom - top)
  return `M${x},${bottom}V${top + r}Q${x},${top} ${x + r},${top}H${x + width - r}Q${x + width},${top} ${x + width},${top + r}V${bottom}Z`
}

const HEIGHT = 220
const PAD = { left: 52, right: 8, top: 12, bottom: 28 }

/**
 * Daily sales as columns: one series, so no legend (the card title names it). Each day's full-height
 * band is the hover/focus target and shows a tooltip; every value is also in the table view.
 */
export function DailySalesChart({ days }) {
  const ref = useRef(null)
  const width = useWidth(ref, 640)
  const [active, setActive] = useState(null)

  const ticks = niceTicks(Math.max(0, ...days.map((d) => Number(d.sales))))
  const top = ticks.at(-1)
  const plotWidth = Math.max(1, width - PAD.left - PAD.right)
  const plotHeight = HEIGHT - PAD.top - PAD.bottom
  const baseline = PAD.top + plotHeight
  const band = plotWidth / days.length
  const barWidth = Math.max(2, Math.min(24, band - 2)) // capped at 24px; at least a 2px surface gap
  const y = (value) => baseline - (Number(value) / top) * plotHeight
  const labelEvery = Math.max(1, Math.ceil(days.length / Math.max(1, Math.floor(plotWidth / 56))))
  const activeDay = active === null ? null : days[active]

  return (
    <div>
      <div ref={ref} className="relative">
        <svg width={width} height={HEIGHT} role="img" aria-label="Daily sales chart" className="block max-w-full">
          {ticks.map((tick) => (
            <g key={tick}>
              <line
                x1={PAD.left}
                x2={width - PAD.right}
                y1={y(tick)}
                y2={y(tick)}
                className="stroke-line"
                strokeWidth="1"
              />
              <text
                x={PAD.left - 8}
                y={y(tick)}
                dy="0.32em"
                textAnchor="end"
                className="fill-ink-muted text-[11px] tabular-nums"
              >
                {formatCompactCurrency(tick)}
              </text>
            </g>
          ))}
          {days.map((day, i) => {
            const x = PAD.left + i * band + (band - barWidth) / 2
            const barTop = y(day.sales)
            return (
              <g key={day.date}>
                {Number(day.sales) > 0 && (
                  <path
                    d={columnPath(x, barTop, barWidth, baseline)}
                    className={`fill-chart-bar transition-opacity ${active !== null && active !== i ? 'opacity-60' : ''}`}
                  />
                )}
                {i % labelEvery === 0 && (
                  <text x={x + barWidth / 2} y={HEIGHT - 8} textAnchor="middle" className="fill-ink-muted text-[11px]">
                    {formatDay(day.date)}
                  </text>
                )}
                <rect
                  x={PAD.left + i * band}
                  y={PAD.top}
                  width={band}
                  height={plotHeight}
                  fill="transparent"
                  tabIndex={0}
                  aria-label={`${formatDay(day.date, { long: true })}: ${formatCurrency(day.sales)} from ${day.orders} orders`}
                  onPointerEnter={() => setActive(i)}
                  onPointerLeave={() => setActive(null)}
                  onFocus={() => setActive(i)}
                  onBlur={() => setActive(null)}
                  className="focus:outline-none"
                />
              </g>
            )
          })}
        </svg>
        {activeDay && (
          <div
            role="tooltip"
            className="border-line bg-surface shadow-card pointer-events-none absolute z-10 w-max -translate-x-1/2 rounded-lg border px-3 py-2 text-xs"
            style={{
              left: Math.min(Math.max(PAD.left + (active + 0.5) * band, 80), width - 80),
              top: Math.max(0, y(activeDay.sales) - 64),
            }}
          >
            <p className="text-sm font-bold">{formatCurrency(activeDay.sales)}</p>
            <p className="text-ink-muted">
              {formatDay(activeDay.date, { long: true })} · {activeDay.orders} orders
              {activeDay.cancelledOrders > 0 && ` · ${activeDay.cancelledOrders} cancelled`}
            </p>
          </div>
        )}
      </div>
      <details className="mt-3 text-sm">
        <summary className="text-brand-700 cursor-pointer font-semibold">Show as table</summary>
        <div className="mt-2 max-h-72 overflow-y-auto">
          <table className="w-full text-left">
            <thead className="text-ink-muted">
              <tr>
                <th className="py-1 font-semibold">Day</th>
                <th className="py-1 text-right font-semibold">Orders</th>
                <th className="py-1 text-right font-semibold">Sales</th>
                <th className="py-1 text-right font-semibold">Cancelled</th>
              </tr>
            </thead>
            <tbody className="divide-line divide-y tabular-nums">
              {days.map((d) => (
                <tr key={d.date}>
                  <td className="py-1">{formatDay(d.date, { long: true })}</td>
                  <td className="py-1 text-right">{d.orders}</td>
                  <td className="py-1 text-right">{formatCurrency(d.sales)}</td>
                  <td className="py-1 text-right">{d.cancelledOrders}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </div>
  )
}

/**
 * A ranked list with a bar under each value (top products, categories). Values are printed, so the
 * bars only add the at-a-glance proportion; the list itself is the table view.
 */
export function RankedBars({ rows, empty = 'Nothing to show for this period.' }) {
  if (rows.length === 0) return <p className="text-ink-muted text-sm">{empty}</p>
  const max = Math.max(...rows.map((r) => Number(r.value)), 0) || 1
  return (
    <ol className="space-y-3">
      {rows.map((row) => (
        <li key={row.key}>
          <div className="flex items-baseline justify-between gap-3 text-sm">
            <span className="min-w-0 truncate">
              <span className="font-semibold">{row.label}</span>
              {row.detail && <span className="text-ink-muted"> · {row.detail}</span>}
            </span>
            <span className="font-semibold whitespace-nowrap tabular-nums">{row.valueLabel}</span>
          </div>
          <div className="mt-1 h-2">
            <div
              className="bg-chart-bar h-2 rounded-r"
              style={{ width: `${Math.max(1, (Number(row.value) / max) * 100)}%` }}
            />
          </div>
        </li>
      ))}
    </ol>
  )
}

/** A headline figure: label, value, and an optional line of context (e.g. "vs ₹4,200 yesterday"). */
export function StatTile({ label, value, detail }) {
  return (
    <div className="rounded-card border-line bg-surface shadow-card border p-4">
      <p className="text-ink-muted text-sm">{label}</p>
      <p className="mt-1 text-2xl font-bold">{value}</p>
      {detail && <p className="text-ink-muted mt-1 text-xs">{detail}</p>}
    </div>
  )
}
