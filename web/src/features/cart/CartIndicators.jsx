import { Link, useLocation } from 'react-router'
import { formatCurrency } from '../../shared/utils/format'
import { useCart } from './useCart'

/** Header cart button with the item count. */
export function CartButton() {
  const { cart } = useCart()
  const count = cart.totals.itemCount
  return (
    <Link
      to="/cart"
      aria-label={count ? `Cart, ${count} item${count === 1 ? '' : 's'}` : 'Cart, empty'}
      className="hover:bg-brand-50 relative grid size-10 shrink-0 place-items-center rounded-lg text-xl"
    >
      <span aria-hidden="true">🛒</span>
      {count > 0 && (
        <span
          aria-hidden="true"
          className="bg-accent-500 absolute -top-0.5 -right-0.5 grid min-w-5 place-items-center rounded-full px-1 text-xs font-bold text-white"
        >
          {count > 99 ? '99+' : count}
        </span>
      )}
    </Link>
  )
}

/**
 * Phones: a sticky "View cart" bar at the bottom of the screen whenever the cart has items, the
 * pattern quick-commerce shoppers expect. Hidden on the cart page itself and on larger screens.
 */
export function CartBar() {
  const { cart } = useCart()
  const { pathname } = useLocation()
  const { itemCount, total } = cart.totals

  if (itemCount === 0 || pathname === '/cart' || pathname.startsWith('/admin')) return null

  return (
    <div className="fixed inset-x-0 bottom-0 z-40 p-3 md:hidden">
      <Link
        to="/cart"
        className="bg-brand-600 shadow-card flex h-14 items-center justify-between rounded-xl px-4 font-semibold text-white"
      >
        <span>
          {itemCount} item{itemCount === 1 ? '' : 's'} · {formatCurrency(total)}
        </span>
        <span>View cart ›</span>
      </Link>
    </div>
  )
}
