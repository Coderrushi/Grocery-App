import { formatCurrency } from '../../shared/utils/format'

/** Product photo, or a soft tile with the aisle's emoji when there is no photo yet. */
export function ProductImage({ product, className = '' }) {
  if (product.imageUrl) {
    return (
      <img
        src={product.imageUrl}
        alt=""
        loading="lazy"
        decoding="async"
        className={`bg-canvas aspect-square w-full rounded-lg object-contain ${className}`}
      />
    )
  }
  return (
    <div
      aria-hidden="true"
      className={`from-brand-50 to-accent-100 grid aspect-square w-full place-items-center rounded-lg bg-gradient-to-br text-5xl ${className}`}
    >
      {product.categoryIcon ?? '🛒'}
    </div>
  )
}

export function PriceTag({ product, size = 'md' }) {
  const hasDiscount = product.discountPercent > 0
  return (
    <div className="flex flex-wrap items-baseline gap-x-2">
      <span className={`font-bold ${size === 'lg' ? 'text-2xl' : 'text-base'}`}>{formatCurrency(product.price)}</span>
      {hasDiscount && (
        <>
          <span className="text-ink-muted text-sm line-through">
            <span className="sr-only">MRP </span>
            {formatCurrency(product.mrp)}
          </span>
          <span className="text-brand-700 text-sm font-semibold">{product.discountPercent}% off</span>
        </>
      )}
    </div>
  )
}

export function StockBadge({ product }) {
  if (product.stockStatus === 'OutOfStock') {
    return <span className="text-danger-700 text-xs font-semibold">Out of stock</span>
  }
  if (product.stockStatus === 'LowStock') {
    return <span className="text-accent-600 text-xs font-semibold">Only {product.onlyLeft} left</span>
  }
  return null
}

export function DiscountRibbon({ product }) {
  if (!product.discountPercent) return null
  return (
    <span className="bg-accent-500 absolute top-2 left-2 rounded-md px-1.5 py-0.5 text-xs font-bold text-white">
      {product.discountPercent}% OFF
    </span>
  )
}
