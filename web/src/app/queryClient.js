import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { currentUserKey } from '../features/auth/useAuth'

// If any request comes back 401, the session has ended (expired, or signed out on another device).
// Clearing the cached user makes protected pages redirect to the login screen.
function handleUnauthorized(error) {
  if (error?.status === 401) queryClient.setQueryData(currentUserKey, null)
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: handleUnauthorized }),
  mutationCache: new MutationCache({
    // A failed login is also a 401, but there's no session to clear in that case — harmless.
    onError: handleUnauthorized,
  }),
  defaultOptions: {
    queries: {
      staleTime: 30_000, // catalogue data changes rarely; avoid refetching on every navigation
      refetchOnWindowFocus: false,
      // Don't retry client errors (4xx) — they won't succeed on a second attempt.
      retry: (failureCount, error) => !(error?.status >= 400 && error?.status < 500) && failureCount < 2,
    },
  },
})
