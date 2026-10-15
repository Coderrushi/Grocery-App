import { useSyncExternalStore } from 'react'

/**
 * The cart of a visitor who hasn't signed in, kept in localStorage as [{ productId, quantity }] only.
 * Prices are never stored here: the API prices this list on every view (/api/cart/preview), and the
 * list is merged into the customer's saved cart when they sign in.
 * Storage can be unavailable (private mode, blocked site data), so every access is guarded and the
 * cart falls back to memory for the current page session.
 */

const STORAGE_KEY = 'qk_guest_cart'
const listeners = new Set()
let items = read()

function read() {
  try {
    const parsed = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]')
    return Array.isArray(parsed)
      ? parsed.filter((i) => Number.isInteger(i?.productId) && Number.isInteger(i?.quantity) && i.quantity > 0)
      : []
  } catch {
    return []
  }
}

function write(next) {
  items = next
  try {
    if (next.length) localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    else localStorage.removeItem(STORAGE_KEY)
  } catch {
    // Storage unavailable: keep the in-memory copy.
  }
  for (const listener of listeners) listener()
}

export const guestCart = {
  getItems: () => items,

  /** Sets a product's quantity; 0 removes it. */
  setQuantity(productId, quantity) {
    const others = items.filter((i) => i.productId !== productId)
    if (quantity <= 0) write(others)
    else if (items.some((i) => i.productId === productId))
      write(items.map((i) => (i.productId === productId ? { productId, quantity } : i)))
    else write([...others, { productId, quantity }])
  },

  clear: () => write([]),

  subscribe(listener) {
    listeners.add(listener)
    return () => listeners.delete(listener)
  },
}

// Keep several open tabs in sync.
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key === STORAGE_KEY) {
      items = read()
      for (const listener of listeners) listener()
    }
  })
}

export function useGuestCartItems() {
  return useSyncExternalStore(guestCart.subscribe, guestCart.getItems)
}
