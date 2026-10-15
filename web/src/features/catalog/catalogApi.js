import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/apiClient'

export const SORT_OPTIONS = [
  { value: 'Relevance', label: 'Recommended' },
  { value: 'PriceLowToHigh', label: 'Price: low to high' },
  { value: 'PriceHighToLow', label: 'Price: high to low' },
  { value: 'BiggestDiscount', label: 'Biggest discount' },
  { value: 'Name', label: 'Name (A–Z)' },
  { value: 'Newest', label: 'Newest' },
]

/** Builds the query string, leaving out empty/default values so cache keys and URLs stay short. */
function toQueryString(params, page) {
  const qs = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '' && value !== false) qs.set(key, String(value))
  }
  qs.set('page', String(page))
  return qs.toString()
}

export function useCategories() {
  return useQuery({
    queryKey: ['catalog', 'categories'],
    queryFn: ({ signal }) => apiClient.get('/catalog/categories', { signal }),
    staleTime: 5 * 60_000, // aisles rarely change
  })
}

/**
 * Products for a grid, fetched page by page as the shopper taps "Show more".
 * `params`: { category, search, featured, inStockOnly, sort, pageSize }
 */
export function useProducts(params) {
  return useInfiniteQuery({
    queryKey: ['catalog', 'products', params],
    queryFn: ({ pageParam, signal }) =>
      apiClient.get(`/catalog/products?${toQueryString(params, pageParam)}`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  })
}

export function useProduct(id) {
  return useQuery({
    queryKey: ['catalog', 'product', id],
    queryFn: ({ signal }) => apiClient.get(`/catalog/products/${id}`, { signal }),
  })
}
