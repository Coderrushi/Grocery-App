import { SORT_OPTIONS } from './catalogApi'

export function ListingControls({ sort, setSort, inStockOnly, setInStockOnly }) {
  return (
    <div className="flex flex-wrap items-center gap-3 text-sm">
      <label className="flex items-center gap-2">
        <span className="text-ink-muted">Sort by</span>
        <select
          value={sort}
          onChange={(e) => setSort(e.target.value)}
          className="border-line bg-surface focus:border-brand-600 h-9 rounded-lg border px-2 focus:outline-none"
        >
          {SORT_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </label>
      <label className="flex items-center gap-2">
        <input
          type="checkbox"
          checked={inStockOnly}
          onChange={(e) => setInStockOnly(e.target.checked)}
          className="accent-brand-600 size-4"
        />
        In stock only
      </label>
    </div>
  )
}
