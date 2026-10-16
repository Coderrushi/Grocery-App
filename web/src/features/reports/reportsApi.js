import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/apiClient'

/** "?from=2026-10-01&to=2026-10-31" (both optional: the API defaults to the last 30 days). */
export function periodQuery({ from, to } = {}) {
  const qs = new URLSearchParams()
  if (from) qs.set('from', from)
  if (to) qs.set('to', to)
  const text = qs.toString()
  return text ? `?${text}` : ''
}

/**
 * CSV downloads are plain GET links: the browser sends the auth cookie and saves the file itself,
 * so no blob handling is needed (apiClient only understands JSON).
 */
export const csvUrl = (path, period) => `/api/admin/reports/${path}${periodQuery(period)}`

export function useDashboard() {
  return useQuery({
    queryKey: ['admin', 'dashboard'],
    queryFn: ({ signal }) => apiClient.get('/admin/dashboard', { signal }),
    refetchInterval: 60_000, // today's numbers keep up while the page is open
  })
}

function useReport(name, period) {
  return useQuery({
    queryKey: ['admin', 'reports', name, period],
    queryFn: ({ signal }) => apiClient.get(`/admin/reports/${name}${periodQuery(period)}`, { signal }),
    placeholderData: keepPreviousData, // changing the period keeps the old figures (dimmed) until the new ones arrive
  })
}

export const useSalesReport = (period) => useReport('sales', period)
export const useInventoryReport = (period) => useReport('inventory', period)
export const useCustomerReport = (period) => useReport('customers', period)
