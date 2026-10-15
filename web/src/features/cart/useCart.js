import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/apiClient'
import { toast } from '../../shared/ui/toast'
import { useCurrentUser } from '../auth/useAuth'
import { guestCart, useGuestCartItems } from './guestCart'

export const cartKey = ['cart']

const emptyCart = {
  lines: [],
  totals: {
    itemCount: 0,
    subtotal: 0,
    mrpTotal: 0,
    savings: 0,
    deliveryFee: 0,
    total: 0,
    amountToFreeDelivery: 0,
    minimumOrderAmount: 0,
    meetsMinimumOrder: false,
  },
  hasIssues: false,
  canCheckout: false,
}

/**
 * The shopper's cart, whoever they are. Signed-in customers: the saved cart from the API. Guests: the
 * browser-held list, priced by the API. Components don't need to know which.
 */
export function useCart() {
  const { data: user, isPending: userPending } = useCurrentUser()
  const signedIn = Boolean(user)
  const guestItems = useGuestCartItems()

  const saved = useQuery({
    queryKey: cartKey,
    queryFn: ({ signal }) => apiClient.get('/cart', { signal }),
    enabled: signedIn,
    staleTime: 0, // always re-check prices and stock when the cart is shown
  })

  const preview = useQuery({
    queryKey: [...cartKey, 'preview', guestItems],
    queryFn: ({ signal }) => apiClient.post('/cart/preview', { items: guestItems }, { signal }),
    enabled: !userPending && !signedIn && guestItems.length > 0,
    placeholderData: keepPreviousData, // no flicker while re-pricing after each tap
  })

  if (signedIn) {
    return { signedIn, cart: saved.data ?? emptyCart, isPending: saved.isPending, error: saved.error }
  }
  if (guestItems.length === 0) {
    return { signedIn, cart: emptyCart, isPending: userPending, error: null }
  }
  return { signedIn, cart: preview.data ?? emptyCart, isPending: preview.isPending, error: preview.error, guestItems }
}

/** The quantity shown on a product's stepper. Guests' taps show instantly from local state. */
export function useCartQuantity(productId) {
  const { cart, guestItems } = useCart()
  const source = guestItems ?? cart.lines.map((l) => ({ productId: l.productId, quantity: l.quantity }))
  return source.find((i) => i.productId === productId)?.quantity ?? 0
}

/**
 * Changes a quantity (0 removes). For signed-in customers the change shows immediately (optimistic)
 * and requests run one after another (mutation scope), so fast taps can never be applied out of order.
 */
export function useSetCartQuantity() {
  const queryClient = useQueryClient()
  const { data: user } = useCurrentUser()

  const mutation = useMutation({
    scope: { id: 'cart' },
    mutationFn: ({ productId, quantity }) =>
      quantity === 0
        ? apiClient.delete(`/cart/items/${productId}`)
        : apiClient.put(`/cart/items/${productId}`, { quantity }),
    onMutate: async ({ productId, quantity }) => {
      await queryClient.cancelQueries({ queryKey: cartKey, exact: true })
      const previous = queryClient.getQueryData(cartKey)
      if (previous?.lines.some((l) => l.productId === productId)) {
        queryClient.setQueryData(cartKey, {
          ...previous,
          lines:
            quantity === 0
              ? previous.lines.filter((l) => l.productId !== productId)
              : previous.lines.map((l) =>
                  l.productId === productId ? { ...l, quantity, lineTotal: l.unitPrice * quantity } : l,
                ),
        })
      }
      return { previous }
    },
    onSuccess: (cart) => queryClient.setQueryData(cartKey, cart),
    onError: (error, _vars, context) => {
      if (context?.previous) queryClient.setQueryData(cartKey, context.previous)
      toast(error.detail ?? error.message, { tone: 'error' })
      queryClient.invalidateQueries({ queryKey: cartKey, exact: true })
    },
  })

  return {
    setQuantity: (productId, quantity) => {
      if (user) mutation.mutate({ productId, quantity })
      else guestCart.setQuantity(productId, quantity)
    },
    isPending: mutation.isPending,
  }
}

export function useClearCart() {
  const queryClient = useQueryClient()
  const { data: user } = useCurrentUser()
  const mutation = useMutation({
    scope: { id: 'cart' },
    mutationFn: () => apiClient.delete('/cart'),
    onSuccess: (cart) => queryClient.setQueryData(cartKey, cart),
  })
  return () => (user ? mutation.mutate() : guestCart.clear())
}
