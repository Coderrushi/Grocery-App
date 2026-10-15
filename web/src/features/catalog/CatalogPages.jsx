import { useEffect } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Spinner } from '../../shared/ui/Spinner'
import { NotFoundPage } from '../../pages/ErrorPages'
import { config } from '../../app/config'
import { useCategories, useProduct } from './catalogApi'
import { CategoryChips } from './CategoryNav'
import { ListingControls } from './ListingControls'
import { useListingParams } from './useListingParams'
import { ProductGrid } from './ProductGrid'
import { DiscountRibbon, PriceTag, ProductImage, StockBadge } from './ProductBits'

function usePageTitle(title) {
  useEffect(() => {
    document.title = title ? `${title} | ${config.storeName}` : config.storeName
  }, [title])
}

export function CategoryPage() {
  const { slug } = useParams()
  const { data: categories, isPending } = useCategories()
  const listing = useListingParams()
  const category = categories?.find((c) => c.slug === slug)
  usePageTitle(category?.name)

  if (isPending) return <Spinner />
  if (!category) return <NotFoundPage />

  return (
    <div className="space-y-4">
      <CategoryChips />
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-bold">
          <span aria-hidden="true">{category.icon} </span>
          {category.name}
        </h1>
        <ListingControls {...listing} />
      </div>
      <ProductGrid
        params={{ category: slug, sort: listing.sort, inStockOnly: listing.inStockOnly }}
        emptyMessage="Nothing here right now. Please check back soon."
      />
    </div>
  )
}

export function SearchPage() {
  const [searchParams] = useSearchParams()
  const query = (searchParams.get('q') ?? '').trim()
  const listing = useListingParams()
  usePageTitle(query ? `Search: ${query}` : 'Search')

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-bold">{query ? <>Results for “{query}”</> : 'Search'}</h1>
        {query && <ListingControls {...listing} />}
      </div>
      {query ? (
        <ProductGrid
          params={{ search: query, sort: listing.sort, inStockOnly: listing.inStockOnly }}
          emptyMessage={`We couldn't find anything for “${query}”. Try a shorter or different word.`}
        />
      ) : (
        <p className="text-ink-muted">Type what you're looking for in the search box above.</p>
      )}
    </div>
  )
}

export function ProductPage() {
  const { id } = useParams()
  const { data, isPending, error } = useProduct(Number(id))
  usePageTitle(data?.product.name)

  if (isPending) return <Spinner />
  if (error?.status === 404) return <NotFoundPage />
  if (error) return <ErrorAlert error={error} title="Couldn't load this product" />

  const { product, description, categoryName } = data

  return (
    <article className="mx-auto max-w-4xl">
      <nav aria-label="Breadcrumb" className="text-ink-muted mb-4 text-sm">
        <Link to="/" className="hover:text-ink">
          Home
        </Link>
        <span aria-hidden="true"> › </span>
        <Link to={`/category/${product.categorySlug}`} className="hover:text-ink">
          {categoryName}
        </Link>
      </nav>
      <div className="grid gap-6 md:grid-cols-2">
        <div className="rounded-card border-line bg-surface relative border p-4">
          <ProductImage product={product} />
          <DiscountRibbon product={product} />
        </div>
        <div>
          {product.brand && <p className="text-ink-muted text-sm">{product.brand}</p>}
          <h1 className="text-2xl font-bold">{product.name}</h1>
          <p className="text-ink-muted mt-1">{product.unitLabel}</p>
          <div className="mt-4">
            <PriceTag product={product} size="lg" />
            {product.discountPercent > 0 && <p className="text-ink-muted text-xs">Inclusive of all taxes</p>}
          </div>
          <div className="mt-2">
            <StockBadge product={product} />
            {product.stockStatus === 'InStock' && (
              <span className="text-success-600 text-xs font-semibold">In stock</span>
            )}
          </div>
          {/* Phase 4: quantity selector and "Add to cart" go here. */}
          {description && (
            <section className="mt-6">
              <h2 className="font-semibold">About this product</h2>
              <p className="text-ink-muted mt-1 whitespace-pre-line">{description}</p>
            </section>
          )}
        </div>
      </div>
    </article>
  )
}
