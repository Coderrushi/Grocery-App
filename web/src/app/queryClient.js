import { QueryClient } from '@tanstack/react-query'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000, // catalogue data changes rarely; avoid refetching on every navigation
      refetchOnWindowFocus: false,
      // Don't retry client errors (4xx) — they won't succeed on a second attempt.
      retry: (failureCount, error) => !(error?.status >= 400 && error?.status < 500) && failureCount < 2,
    },
  },
})
