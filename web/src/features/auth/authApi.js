import { ApiError, apiClient } from '../../shared/api/apiClient'

export const authApi = {
  register: (data) => apiClient.post('/auth/register', data),
  login: (data) => apiClient.post('/auth/login', data),
  logout: () => apiClient.post('/auth/logout'),

  /** The signed-in user, or null when nobody is signed in. */
  me: async ({ signal } = {}) => {
    try {
      return await apiClient.get('/auth/me', { signal })
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) return null
      throw error
    }
  },
}
