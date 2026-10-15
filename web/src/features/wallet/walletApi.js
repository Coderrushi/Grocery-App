import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/apiClient'
import { toast } from '../../shared/ui/toast'

const walletKey = ['wallet']

export function useWallet() {
  return useQuery({
    queryKey: walletKey,
    queryFn: ({ signal }) => apiClient.get('/wallet', { signal }),
    staleTime: 0,
  })
}

/** Transaction history for the signed-in customer, or for `userId` when an admin is viewing. */
export function useWalletTransactions(userId) {
  const base = userId ? `/admin/customers/${userId}/wallet` : '/wallet'
  return useInfiniteQuery({
    queryKey: userId ? ['admin', 'wallet', userId, 'transactions'] : [...walletKey, 'transactions'],
    queryFn: ({ pageParam, signal }) => apiClient.get(`${base}/transactions?page=${pageParam}&pageSize=20`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  })
}

/**
 * A random id for one top-up attempt. The API treats repeats of the same key as the same top-up, so a
 * retried request (e.g. after a dropped connection) can never charge twice.
 * crypto.randomUUID needs a secure context (https or localhost); fall back for plain-http LAN testing.
 */
export function newIdempotencyKey() {
  if (globalThis.crypto?.randomUUID) return crypto.randomUUID()
  return Array.from(crypto.getRandomValues(new Uint8Array(16)), (b) => b.toString(16).padStart(2, '0')).join('')
}

export function useTopUp() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ amount, idempotencyKey }) => apiClient.post('/wallet/top-ups', { amount, idempotencyKey }),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: walletKey })
      if (result.status === 'Pending') {
        // A real gateway confirms asynchronously; the balance updates when it does.
        toast('Your payment is being processed. Your balance will update shortly.')
      }
    },
  })
}

// ---------- Admin ----------

export function useAdminWallet(userId) {
  return useQuery({
    queryKey: ['admin', 'wallet', userId],
    queryFn: ({ signal }) => apiClient.get(`/admin/customers/${userId}/wallet`, { signal }),
    placeholderData: keepPreviousData,
  })
}

export function useAdjustWallet(userId) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data) => apiClient.post(`/admin/customers/${userId}/wallet/adjustments`, data),
    onSuccess: (wallet) => {
      queryClient.setQueryData(['admin', 'wallet', userId], wallet)
      queryClient.invalidateQueries({ queryKey: ['admin', 'wallet', userId, 'transactions'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'customers'] })
    },
  })
}
