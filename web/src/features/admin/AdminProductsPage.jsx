import { useState } from 'react'
import { Link } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Spinner } from '../../shared/ui/Spinner'
import { formatCurrency } from '../../shared/utils/format'
import { useAdminCategories, useAdminProducts } from './adminCatalogApi'

const selectClass =
  'border-line bg-surface focus:border-brand-600 h-10 rounded-lg border px-2 text-sm focus:outline-none'

function StockCell({ product }) {
  const tone =
    product.stockStatus === 'OutOfStock'
      ? 'text-danger-700'
      : product.stockStatus === 'LowStock'
        ? 'text-accent-600'
        : 'text-ink'
  return (
    <span className={`font-semibold ${tone}`}>
      {product.stockQuantity}
      {product.stockStatus === 'LowStock' && <span className="block text-xs font-normal">Low</span>}
      {product.stockStatus === 'OutOfStock' && <span className="block text-xs font-normal">Out</span>}
    </span>
  )
}

export function AdminProductsPage() {
  const [searchInput, setSearchInput] = useState('')
  const [filters, setFilters] = useState({ search: '', categoryId: '', status: 'All', stock: 'All', page: 1 })
  const { data: categories } = useAdminCategories()
  const { data, isPending, isFetching, error } = useAdminProducts(filters)

  const setFilter = (key, value) => setFilters((f) => ({ ...f, [key]: value, page: 1 }))

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-bold">Products</h1>
        <Link
          to="/admin/products/new"
          className="bg-brand-600 hover:bg-brand-700 inline-flex h-11 items-center rounded-lg px-4 font-semibold text-white"
        >
          + New product
        </Link>
      </div>

      <div className="flex flex-wrap gap-2">
        <form
          role="search"
          className="flex gap-2"
          onSubmit={(e) => {
            e.preventDefault()
            setFilter('search', searchInput.trim())
          }}
        >
          <input
            type="search"
            aria-label="Search products"
            placeholder="Name, brand or SKU"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className={`${selectClass} w-56 px-3`}
          />
          <Button type="submit" size="sm" variant="secondary" className="h-10">
            Search
          </Button>
        </form>
        <select
          aria-label="Category"
          className={selectClass}
          value={filters.categoryId}
          onChange={(e) => setFilter('categoryId', e.target.value)}
        >
          <option value="">All categories</option>
          {categories?.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        <select
          aria-label="Status"
          className={selectClass}
          value={filters.status}
          onChange={(e) => setFilter('status', e.target.value)}
        >
          <option value="All">Any status</option>
          <option value="Active">Active</option>
          <option value="Inactive">Inactive</option>
        </select>
        <select
          aria-label="Stock"
          className={selectClass}
          value={filters.stock}
          onChange={(e) => setFilter('stock', e.target.value)}
        >
          <option value="All">Any stock</option>
          <option value="Low">Low stock</option>
          <option value="Out">Out of stock</option>
        </select>
      </div>

      {isPending && <Spinner />}
      {error && <ErrorAlert error={error} title="Couldn't load products" />}

      {data && (
        <>
          <div className="rounded-card border-line bg-surface overflow-x-auto border">
            <table className={`w-full text-left text-sm ${isFetching ? 'opacity-60' : ''}`}>
              <thead className="bg-canvas text-ink-muted">
                <tr>
                  <th className="px-4 py-3 font-semibold">Product</th>
                  <th className="px-4 py-3 font-semibold">Category</th>
                  <th className="px-4 py-3 text-right font-semibold">Price</th>
                  <th className="px-4 py-3 text-right font-semibold">Stock</th>
                  <th className="px-4 py-3 font-semibold">Status</th>
                </tr>
              </thead>
              <tbody className="divide-line divide-y">
                {data.items.map((p) => (
                  <tr key={p.id} className="hover:bg-canvas">
                    <td className="px-4 py-3">
                      <Link to={`/admin/products/${p.id}`} className="flex items-center gap-3">
                        {p.imageUrl ? (
                          <img src={p.imageUrl} alt="" className="size-10 rounded object-cover" />
                        ) : (
                          <span aria-hidden="true" className="bg-canvas grid size-10 place-items-center rounded">
                            📦
                          </span>
                        )}
                        <span>
                          <span className="text-brand-700 font-semibold">{p.name}</span>
                          <span className="text-ink-muted block text-xs">
                            {[p.brand, p.unitLabel, p.sku].filter(Boolean).join(' · ')}
                          </span>
                        </span>
                      </Link>
                    </td>
                    <td className="px-4 py-3">{p.categoryName}</td>
                    <td className="px-4 py-3 text-right whitespace-nowrap">
                      {formatCurrency(p.price)}
                      {p.discountPercent > 0 && (
                        <span className="text-ink-muted block text-xs line-through">{formatCurrency(p.mrp)}</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <StockCell product={p} />
                    </td>
                    <td className="px-4 py-3">
                      {p.isActive ? (
                        <span className="text-success-600">Active</span>
                      ) : (
                        <span className="text-ink-muted">Inactive</span>
                      )}
                      {p.isFeatured && <span className="text-accent-600 block text-xs">★ Featured</span>}
                    </td>
                  </tr>
                ))}
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={5} className="text-ink-muted px-4 py-8 text-center">
                      No products match these filters.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between text-sm">
            <span className="text-ink-muted">{data.totalCount} products</span>
            <div className="flex items-center gap-2">
              <Button
                size="sm"
                variant="secondary"
                disabled={filters.page <= 1}
                onClick={() => setFilters((f) => ({ ...f, page: f.page - 1 }))}
              >
                Previous
              </Button>
              <span>
                Page {data.page} of {Math.max(1, data.totalPages)}
              </span>
              <Button
                size="sm"
                variant="secondary"
                disabled={filters.page >= data.totalPages}
                onClick={() => setFilters((f) => ({ ...f, page: f.page + 1 }))}
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
