import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/apiClient'
import { cartKey } from '../cart/useCart'

export function useCheckoutSummary() {
  return useQuery({
    queryKey: ['checkout'],
    queryFn: ({ signal }) => apiClient.get('/checkout', { signal }),
    staleTime: 0, // always price checkout from fresh data
  })
}

export function usePlaceOrder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request) => apiClient.post('/orders', request),
    onSuccess: (order) => {
      queryClient.setQueryData(['orders', order.id], order)
      // The cart is now empty and the wallet was charged.
      queryClient.invalidateQueries({ queryKey: cartKey })
      queryClient.invalidateQueries({ queryKey: ['wallet'] })
      queryClient.invalidateQueries({ queryKey: ['orders', 'list'] })
      queryClient.removeQueries({ queryKey: ['checkout'] })
    },
    onError: (error) => {
      // Prices, stock or the cart changed: show the customer the up-to-date checkout.
      if (error.status === 409) queryClient.invalidateQueries({ queryKey: ['checkout'] })
    },
  })
}

export function useMyOrders() {
  return useInfiniteQuery({
    queryKey: ['orders', 'list'],
    queryFn: ({ pageParam, signal }) => apiClient.get(`/orders?page=${pageParam}&pageSize=10`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  })
}

export function useOrder(id) {
  return useQuery({
    queryKey: ['orders', id],
    queryFn: ({ signal }) => apiClient.get(`/orders/${id}`, { signal }),
    staleTime: 0,
    refetchInterval: 60_000, // a gentle poll so "Out for delivery" shows up without a manual refresh
  })
}

export function useCancelOrder(id) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reason) => apiClient.post(`/orders/${id}/cancel`, { reason }),
    onSuccess: (order) => {
      queryClient.setQueryData(['orders', id], order)
      queryClient.invalidateQueries({ queryKey: ['orders', 'list'] })
      queryClient.invalidateQueries({ queryKey: ['wallet'] }) // refunded
    },
  })
}

// ---------- Admin ----------

export function useAdminOrders(filters) {
  return useQuery({
    queryKey: ['admin', 'orders', filters],
    queryFn: ({ signal }) => {
      const qs = new URLSearchParams({ page: filters.page, pageSize: 20 })
      if (filters.status) qs.set('status', filters.status)
      if (filters.search) qs.set('search', filters.search)
      return apiClient.get(`/admin/orders?${qs}`, { signal })
    },
    placeholderData: keepPreviousData,
    refetchInterval: 30_000, // new orders appear on their own
  })
}

export function useAdminOrder(id) {
  return useQuery({
    queryKey: ['admin', 'order', id],
    queryFn: ({ signal }) => apiClient.get(`/admin/orders/${id}`, { signal }),
  })
}

export function useChangeOrderStatus(id) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request) => apiClient.post(`/admin/orders/${id}/status`, request),
    onSuccess: (detail) => {
      queryClient.setQueryData(['admin', 'order', id], detail)
      queryClient.invalidateQueries({ queryKey: ['admin', 'orders'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'products'] }) // a cancellation restocks
    },
    onError: (error) => {
      if (error.status === 409) queryClient.invalidateQueries({ queryKey: ['admin', 'order', id] })
    },
  })
}
