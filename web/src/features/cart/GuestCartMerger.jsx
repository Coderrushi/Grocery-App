import { useEffect, useRef } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/apiClient'
import { toast } from '../../shared/ui/toast'
import { useCurrentUser } from '../auth/useAuth'
import { guestCart } from './guestCart'
import { cartKey } from './useCart'

/**
 * When a visitor with items in their browser cart signs in (or registers), moves those items into their
 * saved cart. Lives in the layout, so the auth code doesn't need to know the cart exists.
 */
export function GuestCartMerger() {
  const { data: user } = useCurrentUser()
  const queryClient = useQueryClient()
  const mergedFor = useRef(null) // guards against running twice (React StrictMode, re-renders)

  useEffect(() => {
    const items = guestCart.getItems()
    if (!user || items.length === 0 || mergedFor.current === user.id) return
    mergedFor.current = user.id

    apiClient
      .post('/cart/merge', { items })
      .then((cart) => {
        guestCart.clear()
        queryClient.setQueryData(cartKey, cart)
        toast('We kept the items you added before signing in.', { tone: 'success' })
      })
      .catch(() => {
        mergedFor.current = null // keep the guest items; we'll try again next time
      })
  }, [user, queryClient])

  return null
}
