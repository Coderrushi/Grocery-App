import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Spinner } from '../../shared/ui/Spinner'
import { ProductCard } from './ProductCard'
import { useProducts } from './catalogApi'

/** A paged product grid for any filter combination, with "Show more" (kinder to mobile data than infinite scroll). */
export function ProductGrid({ params, emptyMessage = 'No products found.' }) {
  const { data, error, isPending, fetchNextPage, hasNextPage, isFetchingNextPage } = useProducts(params)

  if (isPending) return <Spinner label="Loading products…" />
  if (error) return <ErrorAlert error={error} title="Couldn't load products" />

  const products = data.pages.flatMap((page) => page.items)
  const total = data.pages[0].totalCount

  if (products.length === 0) {
    return <p className="text-ink-muted py-12 text-center">{emptyMessage}</p>
  }

  return (
    <div>
      <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4 lg:grid-cols-4 xl:grid-cols-5">
        {products.map((product) => (
          <ProductCard key={product.id} product={product} />
        ))}
      </ul>
      <div className="mt-6 flex flex-col items-center gap-2">
        <p className="text-ink-muted text-sm">
          Showing {products.length} of {total}
        </p>
        {hasNextPage && (
          <Button variant="secondary" onClick={() => fetchNextPage()} disabled={isFetchingNextPage}>
            {isFetchingNextPage ? 'Loading…' : 'Show more'}
          </Button>
        )}
      </div>
    </div>
  )
}
