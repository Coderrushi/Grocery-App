import { useQuery } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/apiClient'

export function useApiHealth() {
  return useQuery({
    queryKey: ['system', 'health'],
    queryFn: ({ signal }) => apiClient.get('/health', { signal }),
    retry: false,
  })
}
