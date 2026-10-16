import { useEffect } from 'react'
import { Link } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { Spinner } from '../../shared/ui/Spinner'
import { formatCurrency } from '../../shared/utils/format'
import { config } from '../../app/config'
import { productUrl } from '../catalog/productUrl'
import { ProductImage } from '../catalog/ProductBits'
import { AddToCart } from './AddToCart'
import { BillLines } from './BillLines'
import { useCart, useClearCart, useSetCartQuantity } from './useCart'

function LineIssue({ line }) {
  const { setQuantity } = useSetCartQuantity()
  if (!line.issue) return null

  const message = {
    Unavailable: 'This item is no longer available.',
    OutOfStock: 'This item is out of stock.',
    ExceedsAvailable: `Only ${line.maxQuantity} available. Reduce the quantity to continue.`,
  }[line.issue]

  return (
    <div className="bg-danger-50 text-danger-700 mt-2 flex flex-wrap items-center gap-2 rounded-lg px-3 py-2 text-sm">
      <span className="flex-1">{message}</span>
      {line.issue === 'ExceedsAvailable' ? (
        <Button size="sm" variant="secondary" onClick={() => setQuantity(line.productId, line.maxQuantity)}>
          Change to {line.maxQuantity}
        </Button>
      ) : (
        <Button size="sm" variant="secondary" onClick={() => setQuantity(line.productId, 0)}>
          Remove
        </Button>
      )}
    </div>
  )
}

function CartLine({ line }) {
  const { setQuantity } = useSetCartQuantity()
  const product = { id: line.productId, name: line.name, imageUrl: line.imageUrl, categoryIcon: line.categoryIcon }
  const blocked = line.issue === 'Unavailable' || line.issue === 'OutOfStock'

  return (
    <li className="py-4">
      <div className="flex gap-3">
        <div className={`w-16 shrink-0 sm:w-20 ${line.issue ? 'opacity-50' : ''}`}>
          <ProductImage product={product} />
        </div>
        <div className="min-w-0 flex-1">
          <Link to={productUrl(product)} className="line-clamp-2 font-medium hover:underline">
            {line.name}
          </Link>
          <p className="text-ink-muted text-sm">
            {line.unitLabel} · {formatCurrency(line.unitPrice)}
            {line.unitMrp > line.unitPrice && <span className="ml-1 line-through">{formatCurrency(line.unitMrp)}</span>}
          </p>
          <div className="mt-2 flex items-center justify-between gap-3">
            {blocked ? (
              <span className="text-ink-muted text-sm">Qty {line.quantity}</span>
            ) : (
              <AddToCart product={{ ...product, maxQuantity: line.maxQuantity, stockStatus: line.stockStatus }} />
            )}
            <span className="font-semibold">{formatCurrency(line.lineTotal)}</span>
          </div>
        </div>
        <button
          type="button"
          onClick={() => setQuantity(line.productId, 0)}
          aria-label={`Remove ${line.name}`}
          className="text-ink-muted hover:text-danger-700 self-start p-1"
        >
          ✕
        </button>
      </div>
      <LineIssue line={line} />
    </li>
  )
}

function FreeDeliveryProgress({ totals }) {
  if (totals.amountToFreeDelivery <= 0) {
    return <p className="text-brand-700 text-sm font-semibold">🎉 You've unlocked FREE delivery!</p>
  }
  const threshold = totals.subtotal + totals.amountToFreeDelivery
  const percent = Math.min(100, Math.round((totals.subtotal / threshold) * 100))
  return (
    <div>
      <p className="text-sm">
        Add <strong>{formatCurrency(totals.amountToFreeDelivery)}</strong> more for FREE delivery
      </p>
      <div
        className="bg-brand-100 mt-1 h-2 overflow-hidden rounded-full"
        role="progressbar"
        aria-valuenow={percent}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label="Progress towards free delivery"
      >
        <div className="bg-brand-600 h-full rounded-full" style={{ width: `${percent}%` }} />
      </div>
    </div>
  )
}

function BillSummary({ cart, signedIn }) {
  const { totals } = cart
  return (
    <Card title="Bill details" className="lg:sticky lg:top-24">
      <BillLines totals={totals} />
      <div className="mt-4 space-y-3">
        <FreeDeliveryProgress totals={totals} />
        {!totals.meetsMinimumOrder && (
          <p className="text-accent-600 text-sm">
            Minimum order is {formatCurrency(totals.minimumOrderAmount)}. Add{' '}
            {formatCurrency(totals.minimumOrderAmount - totals.subtotal)} more to place your order.
          </p>
        )}
        {cart.hasIssues && <p className="text-danger-700 text-sm">Please fix the items marked above to continue.</p>}
        {signedIn ? (
          cart.canCheckout ? (
            <Link
              to="/checkout"
              className="bg-brand-600 hover:bg-brand-700 flex h-12 w-full items-center justify-center rounded-lg font-semibold text-white"
            >
              Proceed to checkout
            </Link>
          ) : (
            <Button className="w-full" size="lg" disabled>
              Proceed to checkout
            </Button>
          )
        ) : (
          <Link
            to="/login?returnTo=%2Fcart"
            className="bg-brand-600 hover:bg-brand-700 flex h-11 w-full items-center justify-center rounded-lg font-semibold text-white"
          >
            Sign in to place your order
          </Link>
        )}
      </div>
    </Card>
  )
}

export function CartPage() {
  const { cart, signedIn, isPending, error } = useCart()
  const clear = useClearCart()

  useEffect(() => {
    document.title = `Cart | ${config.storeName}`
  }, [])

  if (isPending) return <Spinner label="Loading your cart…" />
  if (error) return <ErrorAlert error={error} title="Couldn't load your cart" />

  if (cart.lines.length === 0) {
    return (
      <div className="mx-auto max-w-md py-16 text-center">
        <p aria-hidden="true" className="text-6xl">
          🛒
        </p>
        <h1 className="mt-4 text-2xl font-bold">Your cart is empty</h1>
        <p className="text-ink-muted mt-2">Fresh vegetables, dairy, staples and more are waiting for you.</p>
        <Link
          to="/"
          className="bg-brand-600 hover:bg-brand-700 mt-6 inline-flex h-11 items-center rounded-lg px-6 font-semibold text-white"
        >
          Start shopping
        </Link>
      </div>
    )
  }

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
      <section>
        <div className="flex items-baseline justify-between">
          <h1 className="text-2xl font-bold">
            Your cart <span className="text-ink-muted text-base font-normal">({cart.totals.itemCount} items)</span>
          </h1>
          <Button
            size="sm"
            variant="ghost"
            onClick={() => window.confirm('Remove everything from your cart?') && clear()}
          >
            Clear cart
          </Button>
        </div>
        <ul className="divide-line rounded-card border-line bg-surface mt-3 divide-y border px-4">
          {cart.lines.map((line) => (
            <CartLine key={line.productId} line={line} />
          ))}
        </ul>
      </section>
      <BillSummary cart={cart} signedIn={signedIn} />
    </div>
  )
}
