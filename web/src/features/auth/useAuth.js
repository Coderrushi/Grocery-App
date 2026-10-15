import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { authApi } from './authApi'

export const currentUserKey = ['auth', 'me']

/**
 * The signed-in user (or null). TanStack Query's cache is the single source of truth for the session;
 * there is no separate AuthContext to keep in sync.
 */
export function useCurrentUser() {
  return useQuery({
    queryKey: currentUserKey,
    queryFn: ({ signal }) => authApi.me({ signal }),
    staleTime: 5 * 60_000,
  })
}

export function isAdmin(user) {
  return user?.roles?.includes('Admin') ?? false
}

function useSignInMutation(mutationFn) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: (user) => {
      // Drop anything cached for a previous user, then store the new session.
      queryClient.removeQueries({ predicate: (q) => q.queryKey[0] !== 'auth' })
      queryClient.setQueryData(currentUserKey, user)
    },
  })
}

export const useLogin = () => useSignInMutation(authApi.login)
export const useRegister = () => useSignInMutation(authApi.register)

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: authApi.logout,
    onSettled: () => {
      queryClient.clear()
      queryClient.setQueryData(currentUserKey, null)
    },
  })
}
