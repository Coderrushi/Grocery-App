import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/apiClient'

const categoriesKey = ['admin', 'categories']
const productsKey = ['admin', 'products']

/** After any catalogue change, the storefront's cached pages are stale too. */
function useInvalidateCatalog() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['admin'] }),
      queryClient.invalidateQueries({ queryKey: ['catalog'] }),
    ])
}

// ---------- Categories ----------

export function useAdminCategories() {
  return useQuery({
    queryKey: categoriesKey,
    queryFn: ({ signal }) => apiClient.get('/admin/categories', { signal }),
  })
}

export function useSaveCategory() {
  const invalidate = useInvalidateCatalog()
  return useMutation({
    mutationFn: ({ id, ...data }) =>
      id ? apiClient.put(`/admin/categories/${id}`, data) : apiClient.post('/admin/categories', data),
    onSuccess: invalidate,
  })
}

export function useDeleteCategory() {
  const invalidate = useInvalidateCatalog()
  return useMutation({ mutationFn: (id) => apiClient.delete(`/admin/categories/${id}`), onSuccess: invalidate })
}

// ---------- Products ----------

export function useAdminProducts(filters) {
  return useQuery({
    queryKey: [...productsKey, 'list', filters],
    queryFn: ({ signal }) => {
      const qs = new URLSearchParams()
      for (const [key, value] of Object.entries(filters)) if (value) qs.set(key, String(value))
      return apiClient.get(`/admin/products?${qs}`, { signal })
    },
    placeholderData: keepPreviousData,
  })
}

export function useAdminProduct(id) {
  return useQuery({
    queryKey: [...productsKey, id],
    queryFn: ({ signal }) => apiClient.get(`/admin/products/${id}`, { signal }),
    enabled: id != null,
  })
}

/** Writes that return the updated product put it straight into the cache, then refresh lists. */
function useProductMutation(mutationFn) {
  const queryClient = useQueryClient()
  const invalidate = useInvalidateCatalog()
  return useMutation({
    mutationFn,
    onSuccess: (product) => {
      queryClient.setQueryData([...productsKey, product.id], product)
      return invalidate()
    },
  })
}

export const useSaveProduct = () =>
  useProductMutation(({ id, ...data }) =>
    id ? apiClient.put(`/admin/products/${id}`, data) : apiClient.post('/admin/products', data),
  )

export const useUploadProductImage = () =>
  useProductMutation(({ id, file }) => {
    const form = new FormData()
    form.append('file', file)
    return apiClient.post(`/admin/products/${id}/image`, form)
  })

export const useRemoveProductImage = () => useProductMutation((id) => apiClient.delete(`/admin/products/${id}/image`))

export const useAdjustStock = () =>
  useProductMutation(({ id, ...data }) => apiClient.post(`/admin/products/${id}/stock-adjustments`, data))

export function useStockHistory(id) {
  return useQuery({
    queryKey: [...productsKey, id, 'stock-history'],
    queryFn: ({ signal }) => apiClient.get(`/admin/products/${id}/stock-adjustments`, { signal }),
  })
}
