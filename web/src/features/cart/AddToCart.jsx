import { useCartQuantity, useSetCartQuantity } from './useCart'

const sizes = {
  sm: { box: 'h-9', button: 'w-9 text-lg', add: 'h-9 px-4 text-sm' },
  lg: { box: 'h-12', button: 'w-12 text-xl', add: 'h-12 px-8 text-base' },
}

/**
 * "Add" button that turns into a − n + stepper once the product is in the cart.
 * `product` needs { id, name, maxQuantity, stockStatus } (a product card or cart line).
 */
export function AddToCart({ product, size = 'sm', className = '' }) {
  const productId = product.id ?? product.productId
  const quantity = useCartQuantity(productId)
  const { setQuantity, isPending } = useSetCartQuantity()
  const s = sizes[size]
  const max = product.maxQuantity

  if (quantity === 0) {
    const unavailable = max <= 0 || product.stockStatus === 'OutOfStock'
    return (
      <button
        type="button"
        disabled={unavailable || isPending}
        onClick={() => setQuantity(productId, 1)}
        aria-label={unavailable ? `${product.name} is out of stock` : `Add ${product.name} to cart`}
        className={`border-brand-600 text-brand-700 hover:bg-brand-50 disabled:border-line disabled:text-ink-muted relative z-10 rounded-lg border-2 font-bold uppercase disabled:cursor-not-allowed ${s.add} ${className}`}
      >
        {unavailable ? 'Sold out' : isPending ? 'Adding…' : 'Add'}
      </button>
    )
  }

  return (
    <div
      role="group"
      aria-label={`Quantity of ${product.name}`}
      className={`bg-brand-600 relative z-10 inline-flex items-center justify-between rounded-lg font-bold text-white ${s.box} ${className}`}
    >
      <button
        type="button"
        onClick={() => setQuantity(productId, quantity - 1)}
        aria-label={quantity === 1 ? `Remove ${product.name} from cart` : `Decrease ${product.name}`}
        className={`hover:bg-brand-700 h-full rounded-l-lg ${s.button}`}
      >
        −
      </button>
      <span aria-live="polite" className="min-w-8 text-center tabular-nums">
        {quantity}
      </span>
      <button
        type="button"
        onClick={() => setQuantity(productId, quantity + 1)}
        disabled={quantity >= max}
        aria-label={`Increase ${product.name}`}
        title={quantity >= max ? `Maximum ${max} for this item` : undefined}
        className={`hover:bg-brand-700 h-full rounded-r-lg disabled:cursor-not-allowed disabled:opacity-40 ${s.button}`}
      >
        +
      </button>
    </div>
  )
}
