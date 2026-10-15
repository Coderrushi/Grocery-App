import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/apiClient'
import { currentUserKey } from '../auth/useAuth'

const addressesKey = ['account', 'addresses']

// ---------- Profile & password ----------

export function useUpdateProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data) => apiClient.put('/account/profile', data),
    onSuccess: (user) => queryClient.setQueryData(currentUserKey, user),
  })
}

export function useChangePassword() {
  return useMutation({ mutationFn: (data) => apiClient.post('/account/change-password', data) })
}

// ---------- Addresses ----------

export function useAddresses() {
  return useQuery({
    queryKey: addressesKey,
    queryFn: ({ signal }) => apiClient.get('/account/addresses', { signal }),
  })
}

export function useAddress(id) {
  const queryClient = useQueryClient()
  return useQuery({
    queryKey: [...addressesKey, id],
    queryFn: ({ signal }) => apiClient.get(`/account/addresses/${id}`, { signal }),
    enabled: id != null,
    // Show the copy from the list instantly when we have it; the request then refreshes it.
    initialData: () => queryClient.getQueryData(addressesKey)?.find((a) => a.id === id),
  })
}

function useAddressMutation(mutationFn) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: addressesKey }),
  })
}

export const useSaveAddress = () =>
  useAddressMutation(({ id, ...data }) =>
    id ? apiClient.put(`/account/addresses/${id}`, data) : apiClient.post('/account/addresses', data),
  )
export const useDeleteAddress = () => useAddressMutation((id) => apiClient.delete(`/account/addresses/${id}`))
export const useSetDefaultAddress = () => useAddressMutation((id) => apiClient.post(`/account/addresses/${id}/default`))
